using System.Reflection;
using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Diagnostics;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.LogStructure;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Utils.ByteUtils;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction.Audit
{
    /// <summary>
    /// 审计探针。断言写的是「不应发生损坏 / 假成功」的契约。
    /// 断言写的是已经用失败用例钉住的契约。修复后这些用例应通过。
    /// </summary>
    [TestClass]
    public class InternalHazardAuditTests
    {
        const string Table = "audit";

        [TestMethod]
        public void EmptyInsert_IsNotSuccess()
        {
            using var eng = new DbEngine();
            using var ts = eng.StartTransaction();
            ts.Create(Table, [("sku", DbValueType.Int, true)]);

            var inserted = ts.Insert(Table, Array.Empty<(string columnName, DbCell value)>());

            Assert.IsFalse(inserted.IsSuccess,
                $"空插入返回成功 id={inserted.Value}。调用方会把它当成已写入的行号。");
            Assert.AreEqual(0, Count(ts));
        }

        [TestMethod]
        public void FileHeaderState_AfterSaveChanges_IsDone()
        {
            var path = Configuration.GetRandomPath();
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                using (var ts = eng.StartTransaction())
                {
                    Seed(ts);
                    ts.SaveChanges();
                }

                Assert.AreEqual((byte)DbLogState.Done, ReadStateByte(path),
                    "SaveChanges 回放结束后，主库状态字节应为 Done。");
                Assert.IsFalse(File.Exists(path + ".log"),
                    "成功提交后 .log 仍残留。");
            }
            finally
            {
                Cleanup(eng, path);
            }
        }

        [TestMethod]
        public void MemoryHeaderState_AfterFileSave_ReturnsToDone()
        {
            var path = Configuration.GetRandomPath();
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                using var ts = eng.StartTransaction();
                Seed(ts);
                ts.SaveChanges();

                var memoryState = ReadMemoryHeaderState(ts);
                var fileState = ReadStateByte(path);
                Assert.AreEqual((byte)DbLogState.Done, memoryState,
                    $"文件状态字节={fileState}（{(DbLogState)fileState}），内存 header.State={memoryState}（{(DbLogState)memoryState}）。" +
                    "DbLog.Write(DbHeader) 把活对象改成 Writing 后，回放只改文件、不改内存。");
            }
            finally
            {
                Cleanup(eng, path);
            }
        }

        [TestMethod]
        public void SaveAs_AfterFileSave_ProducesOpenableDoneFile()
        {
            var path = Configuration.GetRandomPath();
            var copy = path + ".saveas";
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                using (var ts = eng.StartTransaction())
                {
                    Seed(ts);
                    ts.SaveChanges();
                    ((LumTransaction)ts).SaveAs(copy);
                }

                var state = ReadStateByte(copy);
                using var opened = new DbEngine(copy, createIfNotExists: false);
                using var check = opened.StartTransaction();
                Assert.IsTrue(check.Find(Table, "sku", 1).IsSuccess,
                    $"SaveAs 文件能打开但读不到行。状态字节={state}（{(DbLogState)state}）。");
                Assert.AreEqual((byte)DbLogState.Done, state,
                    "SaveAs 写出的文件能打开，但状态字节不是 Done。");
                opened.SetDestoryOnDisposed();
            }
            catch (Exception ex) when (ex is not AssertFailedException)
            {
                byte state = 0;
                try { state = ReadStateByte(copy); } catch { /* 构造失败时句柄可能尚未释放 */ }
                Assert.Fail(
                    $"SaveChanges 之后 SaveAs 的文件无法打开。副本状态字节={state}（{(DbLogState)state}）。" +
                    $"异常：{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Cleanup(eng, path);
                TryDelete(copy);
                TryDelete(copy + ".log");
            }
        }

        [TestMethod]
        public void Open_WhenStateByteWritingAndLogMissing_StillReadsIntactPages()
        {
            var path = Configuration.GetRandomPath();
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                using (var ts = eng.StartTransaction())
                {
                    Seed(ts);
                    ts.SaveChanges();
                }
                eng.Dispose();
                eng = null;

                var bytes = File.ReadAllBytes(path);
                bytes[DbHeader.STATE_POS] = (byte)DbLogState.Writing;
                File.WriteAllBytes(path, bytes);
                TryDelete(path + ".log");

                using var reopened = new DbEngine(path, createIfNotExists: false);
                using var ts2 = reopened.StartTransaction();
                Assert.IsTrue(ts2.Find(Table, "sku", 1).IsSuccess,
                    "只把状态字节改成 Writing、页内容未改、且没有 .log 时，库应仍能读出原数据。");
                Assert.AreEqual((byte)DbLogState.Done, ReadStateByte(path),
                    "没有可回放的 log 时，打开应把状态字节修回 Done。");
                Assert.AreEqual((byte)DbLogState.Done, ReadMemoryHeaderState(ts2));
                reopened.SetDestoryOnDisposed();
            }
            catch (Exception ex) when (ex is not AssertFailedException)
            {
                Assert.Fail(
                    "主库 STATE=Writing 且 .log 不存在时打开失败，页数据本身仍在：" +
                    $"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Cleanup(eng, path);
            }
        }

        [TestMethod]
        public void FileStream_IsOpenedWriteThrough()
        {
            var path = Configuration.GetRandomPath();
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                var options = ReadFileOptions(eng);
                Assert.IsTrue(options.HasFlag(FileOptions.WriteThrough),
                    $"主库 FileStream 选项为 {options}，没有 WriteThrough。SaveChanges 的 Flush() 只进入 OS 缓存。");
            }
            finally
            {
                Cleanup(eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void LaterColumnTypeError_DoesNotKeepEarlierColumnChanges(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using var ts = eng.StartTransaction();
                SeedThreeColumns(ts);
                var db = ((LumTransaction)ts).TransactionDb;
                var table = TableRepoManager.GetTablePage(db, Table)!;
                var node = TableManager.FirstOrDefaultNode(db, table, "sku", 1);
                Assert.IsNotNull(node);

                var thrown = false;
                try
                {
                    TableManager.Update(db, table, node!,
                    [
                        DbCell.FromInt(2),
                        DbCell.FromString("new"),
                        DbCell.FromString("bad"),
                    ]);
                }
                catch (Exception ex)
                {
                    thrown = true;
                    Assert.IsTrue(ex.Message.Length > 0);
                }

                Assert.IsTrue(thrown, "第三列类型不匹配时应在写入循环中失败。");
                Assert.AreEqual(1, BitConverter.ToInt32(node!.Data.Slice(0, 4)),
                    "类型检查应发生在改写行字节之前，sku 的 4 字节仍应是 1。");
                var host = PageManager.GetPage<DataPage>(db, node.HostPageId);
                Assert.AreEqual(1, host.CurrentDataCount);
                Assert.IsTrue(host.DataNodes[node.NodeIndex].IsAvailable);

                var oldKey = ts.Find(Table, "sku", 1);
                var newKey = ts.Find(Table, "sku", 2);
                Assert.IsTrue(oldKey.IsSuccess,
                    "后列失败后，前面的列已经改完且没有 Discard。" +
                    $" sku=1 success={oldKey.IsSuccess}; sku=2 success={newKey.IsSuccess}" +
                    (newKey.IsSuccess ? $", note={newKey.Row.GetString(1)}" : "") +
                    "。Update/UpdateEntity 没有 catch。");
                if (oldKey.IsSuccess)
                    Assert.AreEqual("old", oldKey.Row.GetString(1));

                ts.Discard();
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void LaterColumnTypeError_DisposeDoesNotCommitPartialUpdate(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                using (var ts = eng.StartTransaction())
                {
                    SeedThreeColumns(ts);
                    var db = ((LumTransaction)ts).TransactionDb;
                    var table = TableRepoManager.GetTablePage(db, Table)!;
                    var node = TableManager.FirstOrDefaultNode(db, table, "sku", 1);
                    try
                    {
                        TableManager.Update(db, table, node!,
                        [
                            DbCell.FromInt(2),
                            DbCell.FromString("new"),
                            DbCell.FromString("bad"),
                        ]);
                    }
                    catch (Exception)
                    {
                        // 与 Update/UpdateEntity 一样：不 Discard，交给 Dispose。
                    }
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    var oldKey = ts.Find(Table, "sku", 1);
                    var newKey = ts.Find(Table, "sku", 2);
                    Assert.IsTrue(oldKey.IsSuccess && oldKey.Row.GetString(1) == "old",
                        "Dispose 把半更新提交了。" +
                        $" sku=1 success={oldKey.IsSuccess}; sku=2 success={newKey.IsSuccess}" +
                        (newKey.IsSuccess ? $", note={newKey.Row.GetString(1)}, n={(newKey.Row.GetInt(2))}" : ""));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        public void Create_WhenAvailableTableRepoInvalid_DoesNotReportSuccess()
        {
            var path = Configuration.GetRandomPath();
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                using (var ts = eng.StartTransaction())
                {
                    Seed(ts);
                    ts.SaveChanges();
                }
                eng.Dispose();
                eng = null;

                PatchUInt32(path, AvailableTableRepoOffset, uint.MaxValue);

                using var reopened = new DbEngine(path, createIfNotExists: false);
                using var ts2 = reopened.StartTransaction();
                var created = ts2.Create("ghost", [("a", DbValueType.Int, true)]);
                var names = ts2.GetTableNames().Values.Select(v => v.tableName).ToArray();
                Assert.IsTrue(created.IsSuccess && names.Contains("ghost"),
                    $"AvailableTableRepo 无效时 Create.IsSuccess={created.IsSuccess}，" +
                    $"现有表=[{string.Join(",", names)}]。根仓库页仍有效，应修好指针并真正建表。");
                var cache = ((LumTransaction)ts2).TransactionDb;
                Assert.IsTrue(cache.IsValidPage(cache.AvailableTableRepo));
                Assert.AreNotEqual(uint.MaxValue, TableRepoManager.FindTablePageId(cache, "ghost"));
                reopened.SetDestoryOnDisposed();
            }
            finally
            {
                Cleanup(eng, path);
            }
        }

        [TestMethod]
        [DataRow(TestBackend.Memory)]
        [DataRow(TestBackend.File)]
        public void DeleteLastRow_ThenReusePage_KeepsNewVarPayload(TestBackend backend)
        {
            var eng = Configuration.Create(backend, out var path);
            try
            {
                const string oldA = "OLD-A-";
                const string oldB = "OLD-B-";
                var payloadA = oldA + new string('a', 8000);
                var payloadB = oldB + new string('b', 8000);
                var nextA = "NEW-A-" + new string('c', 8000);
                var nextB = "NEW-B-" + new string('d', 8000);

                using (var ts = eng.StartTransaction())
                {
                    ts.Create(Table, [
                        ("sku", DbValueType.Int, true),
                        ("a", DbValueType.StrVar, false),
                        ("b", DbValueType.StrVar, false),
                    ]);
                    var inserted = ts.Insert(Table, [("sku", 1), ("a", payloadA), ("b", payloadB)]);
                    Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
                    var deleted = ts.Delete(Table, "sku", 1);
                    Assert.IsTrue(deleted.IsSuccess, deleted.Exception?.Message);

                    var metrics = DbDiagnostics.Inspect(((LumTransaction)ts).TransactionDb);
                    Assert.AreEqual(0, metrics.VarNodesLive,
                        $"删掉唯一一行后仍有 {metrics.VarNodesLive} 个存活 Var 节点，dead={metrics.VarNodesDead}。");

                    var again = ts.Insert(Table, [("sku", 2), ("a", nextA), ("b", nextB)]);
                    Assert.IsTrue(again.IsSuccess, again.Exception?.Message);
                    var row = ts.Find(Table, "sku", 2);
                    Assert.IsTrue(row.IsSuccess);
                    Assert.AreEqual(nextA, row.Row.GetString(1));
                    Assert.AreEqual(nextB, row.Row.GetString(2));
                    ts.SaveChanges();
                }

                Configuration.VerifyCommitted(backend, eng, path, ts =>
                {
                    Assert.IsFalse(ts.Find(Table, "sku", 1).IsSuccess);
                    var row = ts.Find(Table, "sku", 2);
                    Assert.IsTrue(row.IsSuccess);
                    Assert.AreEqual(nextA, row.Row.GetString(1));
                    Assert.AreEqual(nextB, row.Row.GetString(2));
                });
            }
            finally
            {
                Configuration.Cleanup(backend, eng, path);
            }
        }

        [TestMethod]
        [DataRow(TransactionPolicy.ReadCommitted)]
        [DataRow(TransactionPolicy.Serializable)]
        public void SameThread_SecondWriteTransaction_DoesNotLoseCommittedRow(TransactionPolicy policy)
        {
            var path = Configuration.GetRandomPath();
            DbEngine? eng = null;
            try
            {
                eng = new DbEngine(path);
                eng.TransactionPolicy = policy;
                eng.TimeoutMilliseconds = 200;

                using (var first = eng.StartTransaction())
                {
                    Seed(first);
                    first.SaveChanges();

                    try
                    {
                        using var second = eng.StartTransaction();
                        Assert.Fail($"{policy}：同线程第二个写事务启动成功，两份缓存会互相覆盖。");
                    }
                    catch (Exception ex)
                    {
                        Assert.AreEqual(LumExceptionMessage.IllegaTransaction, ex.Message);
                    }

                    var third = first.Insert(Table, [("sku", 3), ("note", "from-first")]);
                    Assert.IsTrue(third.IsSuccess, third.Exception?.Message);
                    first.SaveChanges();
                    Assert.IsTrue(first.Find(Table, "sku", 1).IsSuccess);
                    Assert.IsFalse(first.Find(Table, "sku", 2).IsSuccess);
                }

                using (var sequential = eng.StartTransaction())
                {
                    var inserted = sequential.Insert(Table, [("sku", 2), ("note", "from-second")]);
                    Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
                    sequential.SaveChanges();
                }

                eng.Dispose();
                eng = null;
                using var reopened = new DbEngine(path, createIfNotExists: false);
                using var check = reopened.StartTransaction();
                Assert.IsTrue(check.Find(Table, "sku", 1).IsSuccess, policy.ToString());
                Assert.IsTrue(check.Find(Table, "sku", 2).IsSuccess, policy.ToString());
                Assert.IsTrue(check.Find(Table, "sku", 3).IsSuccess, policy.ToString());
                reopened.SetDestoryOnDisposed();
            }
            finally
            {
                Cleanup(eng, path);
            }
        }

        [TestMethod]
        public void OtherThread_SecondWriteTransaction_TimesOut()
        {
            using var eng = new DbEngine();
            eng.TimeoutMilliseconds = 200;
            using var first = eng.StartTransaction();
            Seed(first);

            Exception? error = null;
            var started = false;
            var worker = Task.Run(() =>
            {
                try
                {
                    using var second = eng.StartTransaction();
                    started = true;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            Assert.IsTrue(worker.Wait(5000));
            Assert.IsFalse(started, "另一个线程在已有写事务时仍启动了写事务。");
            Assert.IsNotNull(error);
            Assert.IsTrue(error!.Message.Contains("time out", StringComparison.OrdinalIgnoreCase), error.Message);
        }

        [TestMethod]
        public void OtherThread_SaveChanges_TimesOutWhileReadonlyTransactionIsOpen()
        {
            using var eng = new DbEngine();
            eng.TimeoutMilliseconds = 200;
            using (var ts = eng.StartTransaction())
            {
                Seed(ts);
                ts.SaveChanges();
            }

            var readerReady = new ManualResetEventSlim(false);
            var releaseReader = new ManualResetEventSlim(false);
            Exception? readerError = null;
            var readerTask = Task.Run(() =>
            {
                try
                {
                    using var reader = eng.StartTransactionReadonly();
                    if (!reader.Find(Table, "sku", 1).IsSuccess)
                        throw new InvalidOperationException("readonly find failed");
                    readerReady.Set();
                    if (!releaseReader.Wait(5000))
                        throw new TimeoutException("reader was not released");
                }
                catch (Exception ex)
                {
                    readerError = ex;
                    readerReady.Set();
                }
            });

            Assert.IsTrue(readerReady.Wait(5000));
            Assert.IsNull(readerError, readerError?.ToString());

            var blocked = false;
            string? saveError = null;
            var writerTask = Task.Run(() =>
            {
                try
                {
                    using var writer = eng.StartTransaction();
                    var updated = writer.Update(Table, "sku", 1, "note", "changed-under-reader");
                    if (!updated.IsSuccess)
                        throw new InvalidOperationException(updated.Exception?.Message);
                    writer.SaveChanges();
                }
                catch (Exception ex)
                {
                    saveError = ex.Message;
                    blocked = ex.Message.Contains("time out", StringComparison.OrdinalIgnoreCase);
                }
            });

            Assert.IsTrue(writerTask.Wait(5000));
            releaseReader.Set();
            Assert.IsTrue(readerTask.Wait(5000));
            Assert.IsTrue(blocked, $"另一线程持有只读事务时 SaveChanges 没有超时。异常：{saveError}");
        }

        [TestMethod]
        public unsafe void FixedStackallocMemoryStream_Read_CountAboveRemaining_ReturnsActualLength()
        {
            var raw = stackalloc byte[32];
            for (var i = 0; i < 32; i++)
                raw[i] = (byte)(i + 1);

            using var stream = new FixedStackallocMemoryStream(raw, 32);
            var dest = new byte[64];
            int n;
            try
            {
                n = stream.Read(dest, 0, 40);
            }
            catch (Exception ex)
            {
                Assert.Fail(
                    "FixedStackallocMemoryStream.Read(byte[]) 在 n>8 时用 count 而不是实际可读长度切片：" +
                    $"{ex.GetType().Name}: {ex.Message}");
                return;
            }

            Assert.AreEqual(32, n);
            CollectionAssert.AreEqual(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray(), dest.Take(32).ToArray());
        }

        [TestMethod]
        public unsafe void FixedStackallocMemoryStream_Read_HonorsOffsetAndPosition()
        {
            var raw = stackalloc byte[32];
            for (var i = 0; i < 32; i++)
                raw[i] = (byte)(i + 1);

            using var stream = new FixedStackallocMemoryStream(raw, 32);
            for (var i = 0; i < 4; i++)
                Assert.AreEqual(i + 1, stream.ReadByte());

            var dest = new byte[32];
            Array.Fill(dest, (byte)0xEE);
            var n = stream.Read(dest, 2, 10);

            Assert.AreEqual(10, n);
            Assert.AreEqual((byte)0xEE, dest[0],
                "Read 把数据写到了 buffer[0]，没有使用调用方传入的 offset。");
            Assert.AreEqual((byte)0xEE, dest[1]);
            Assert.AreEqual(raw[4], dest[2], "应从当前 Position 拷贝，而不是从 offset 指向的源位置拷贝。");
            Assert.AreEqual(raw[13], dest[11]);
        }

        static void Seed(ITransaction ts)
        {
            ts.Create(Table, [("sku", DbValueType.Int, true), ("note", DbValueType.StrVar, false)]);
            var inserted = ts.Insert(Table, [("sku", 1), ("note", "keep")]);
            Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
        }

        static void SeedThreeColumns(ITransaction ts)
        {
            ts.Create(Table, [
                ("sku", DbValueType.Int, true),
                ("note", DbValueType.StrVar, false),
                ("n", DbValueType.Int, false),
            ]);
            var inserted = ts.Insert(Table, [("sku", 1), ("note", "old"), ("n", 7)]);
            Assert.IsTrue(inserted.IsSuccess, inserted.Exception?.Message);
        }

        static int Count(ITransaction ts)
        {
            var n = 0;
            ts.GoThrough(Table, (ref RowView _) =>
            {
                n++;
                return true;
            });
            return n;
        }

        const int AvailableTableRepoOffset = 14;

        static void PatchUInt32(string path, int offset, uint value)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            fs.Seek(offset, SeekOrigin.Begin);
            Span<byte> bytes = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            fs.Write(bytes);
            fs.Flush(true);
        }

        static byte ReadStateByte(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(DbHeader.STATE_POS, SeekOrigin.Begin);
            return (byte)fs.ReadByte();
        }

        static byte ReadMemoryHeaderState(ITransaction ts)
        {
            var cache = ((LumTransaction)ts).TransactionDb;
            var header = typeof(DbCache).GetField("header", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(cache)!;
            return (byte)header.GetType().GetProperty("State")!.GetValue(header)!;
        }

        static FileOptions ReadFileOptions(DbEngine eng)
        {
            var iof = typeof(DbEngine).GetField("iof", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(eng)!;
            var stream = (FileStream)iof.GetType().GetField("fileStream", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(iof)!;
            if (TryReadFileOptions(stream, out var options))
                return options;

            Assert.Fail("读不到 FileStream 的 FileOptions，无法确认是否 WriteThrough。");
            return FileOptions.None;
        }

        static bool TryReadFileOptions(object target, out FileOptions options)
        {
            var seen = new HashSet<object>();
            return TryReadFileOptions(target, seen, out options);
        }

        static bool TryReadFileOptions(object target, HashSet<object> seen, out FileOptions options)
        {
            options = FileOptions.None;
            if (target == null || !seen.Add(target))
                return false;

            for (var type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(FileOptions))
                    {
                        options = (FileOptions)field.GetValue(target)!;
                        return true;
                    }

                    if (field.FieldType.IsPrimitive || field.FieldType == typeof(string))
                        continue;

                    var child = field.GetValue(target);
                    if (child != null && TryReadFileOptions(child, seen, out options))
                        return true;
                }
            }

            return false;
        }

        static void Cleanup(DbEngine? eng, string path)
        {
            try { eng?.SetDestoryOnDisposed(); } catch { /* already disposed */ }
            try { eng?.Dispose(); } catch { /* already disposed */ }
            TryDelete(path);
            TryDelete(path + ".log");
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 测试清理失败不影响断言结果
            }
        }
    }
}
