using System.Collections.Concurrent;
using LumDbEngine;
using LumDbEngine.Element.Engine.Transaction;
using UnitTestLumDb.Config;

namespace UnitTestLumDb.BaseFunction
{
    /// <summary>
    /// Same write-transaction, many threads. Each terminal (ToList/Count/Insert/…) takes
    /// the per-transaction r/w lock, so a query never observes a half-written row.
    /// Each thread must build its own Query() chain (the queryable itself is not shared).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public partial class EntityQueryConcurrentTxnTests
    {
        [LumEntity]
        public partial class CxUser
        {
            [Id] public uint Id { get; set; }
            [Key] public int Code { get; set; }
            public int Score { get; set; }
            public bool Active { get; set; }
        }

        [LumEntity]
        public partial class CxOrder
        {
            [Id] public uint Id { get; set; }
            public int UserCode { get; set; }
            public int Amount { get; set; }
        }

        private static void Create(ITransaction ts)
        {
            ts.Create<CxUser>("users");
            ts.Create<CxOrder>("orders");
        }

        private static void SeedUsers(ITransaction ts, int n)
        {
            for (int i = 0; i < n; i++)
                ts.Insert("users", new CxUser { Code = i, Score = i, Active = i % 2 == 0 });
        }

        private static void SeedOrders(ITransaction ts, int n)
        {
            for (int i = 0; i < n; i++)
                ts.Insert("orders", new CxOrder { UserCode = i, Amount = 10 + i });
        }

        private static void ThrowIfAny(ConcurrentBag<Exception> errors)
        {
            if (errors.IsEmpty)
                return;
            throw new AggregateException(errors);
        }

        [TestMethod]
        public void ParallelQuery_NoWriter_SameCounts()
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 15000;
            using var ts = eng.StartTransaction();
            Create(ts);
            SeedUsers(ts, 80);
            SeedOrders(ts, 80);

            var errors = new ConcurrentBag<Exception>();
            Parallel.For(0, 32, _ =>
            {
                try
                {
                    Assert.AreEqual(80, ts.Query<CxUser>("users").Count());
                    Assert.AreEqual(40, ts.Query<CxUser>("users").Where(u => u.Active).Count());
                    var page = ts.Query<CxUser>("users").Where(u => u.Score >= 70).OrderBy(u => u.Code).ToList();
                    CollectionAssert.AreEqual(Enumerable.Range(70, 10).ToArray(), page.Select(u => u.Code).ToArray());
                    Assert.AreEqual(80, ts.Query<CxUser>("users")
                        .Join<CxOrder>("orders", (u, o) => u.Code == o.UserCode)
                        .Count());
                    Assert.AreEqual(40, ts.Query<CxUser>("users")
                        .WhereExists<CxOrder>("orders", (u, o) => u.Code == o.UserCode && o.Amount >= 50)
                        .Count());
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });
            ThrowIfAny(errors);
        }

        [TestMethod]
        public void ParallelInsert_ThenParallelQuery()
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 15000;
            using var ts = eng.StartTransaction();
            Create(ts);

            var errors = new ConcurrentBag<Exception>();
            Parallel.For(0, 120, i =>
            {
                try
                {
                    Assert.IsTrue(ts.Insert("users", new CxUser { Code = i, Score = i * 2, Active = true }).IsSuccess);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });
            ThrowIfAny(errors);

            Parallel.For(0, 16, _ =>
            {
                try
                {
                    Assert.AreEqual(120, ts.Query<CxUser>("users").Count());
                    Assert.AreEqual(60, ts.Query<CxUser>("users").Where(u => u.Code % 2 == 0).Count());
                    Assert.AreEqual(238, ts.Query<CxUser>("users").Where(u => u.Code == 119).First().Score);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });
            ThrowIfAny(errors);
        }

        [TestMethod]
        public void InsertAndQuery_Interleaved()
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 15000;
            using var ts = eng.StartTransaction();
            Create(ts);

            const int n = 150;
            var errors = new ConcurrentBag<Exception>();

            var writer = Task.Run(() =>
            {
                Parallel.For(0, n, i =>
                {
                    try
                    {
                        ts.Insert("users", new CxUser { Code = i, Score = i, Active = i % 2 == 0 });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            var reader = Task.Run(() =>
            {
                Parallel.For(0, 300, _ =>
                {
                    try
                    {
                        var c = ts.Query<CxUser>("users").Count();
                        Assert.IsTrue(c >= 0 && c <= n, $"Count {c} out of range");
                        var list = ts.Query<CxUser>("users").Where(u => u.Active).Take(8).ToList();
                        Assert.IsTrue(list.Count <= 8);
                        Assert.IsTrue(list.All(u => u.Active));
                        ts.Query<CxUser>("users").Where(u => u.Code == 0).Exists();
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            Assert.IsTrue(Task.WaitAll([writer, reader], 20000));
            ThrowIfAny(errors);
            Assert.AreEqual(n, ts.Query<CxUser>("users").Count());
            Assert.AreEqual(n / 2, ts.Query<CxUser>("users").Where(u => u.Active).Count());
        }

        [TestMethod]
        public void InsertAndJoin_Interleaved()
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 15000;
            using var ts = eng.StartTransaction();
            Create(ts);
            SeedUsers(ts, 60);

            var errors = new ConcurrentBag<Exception>();
            var writer = Task.Run(() =>
            {
                Parallel.For(0, 60, i =>
                {
                    try
                    {
                        ts.Insert("orders", new CxOrder { UserCode = i, Amount = 20 + i });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            var reader = Task.Run(() =>
            {
                Parallel.For(0, 120, _ =>
                {
                    try
                    {
                        var pairs = ts.Query<CxUser>("users")
                            .Join<CxOrder>("orders", (u, o) => u.Code == o.UserCode)
                            .Where((u, o) => o.Amount >= 20)
                            .ToList();
                        Assert.IsTrue(pairs.Count >= 0 && pairs.Count <= 60);
                        Assert.IsTrue(pairs.All(p => p.Outer.Code == p.Inner.UserCode));

                        var exists = ts.Query<CxUser>("users")
                            .WhereExists<CxOrder>("orders", (u, o) => u.Code == o.UserCode && o.Amount >= 50)
                            .Count();
                        Assert.IsTrue(exists >= 0 && exists <= 60);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            Assert.IsTrue(Task.WaitAll([writer, reader], 20000));
            ThrowIfAny(errors);
            Assert.AreEqual(60, ts.Query<CxUser>("users").Join<CxOrder>("orders", (u, o) => u.Code == o.UserCode).Count());
        }

        [TestMethod]
        public void UpdateAndQuery_Interleaved()
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 15000;
            using var ts = eng.StartTransaction();
            Create(ts);
            SeedUsers(ts, 80);

            var errors = new ConcurrentBag<Exception>();
            var writer = Task.Run(() =>
            {
                Parallel.For(0, 80, i =>
                {
                    try
                    {
                        ts.UpdateEntity("users", "Code", i, new CxUser { Code = i, Score = 1000 + i, Active = true });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            var reader = Task.Run(() =>
            {
                Parallel.For(0, 160, _ =>
                {
                    try
                    {
                        var rows = ts.Query<CxUser>("users").Where(u => u.Score >= 1000).ToList();
                        Assert.IsTrue(rows.Count <= 80);
                        Assert.IsTrue(rows.All(u => u.Score == 1000 + u.Code));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            Assert.IsTrue(Task.WaitAll([writer, reader], 20000));
            ThrowIfAny(errors);
            Assert.AreEqual(80, ts.Query<CxUser>("users").Where(u => u.Score >= 1000).Count());
        }

        [TestMethod]
        public void DeleteAndQuery_Interleaved()
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 15000;
            using var ts = eng.StartTransaction();
            Create(ts);
            SeedUsers(ts, 100);

            var errors = new ConcurrentBag<Exception>();
            var writer = Task.Run(() =>
            {
                Parallel.For(0, 50, i =>
                {
                    try
                    {
                        var n = ts.Query<CxUser>("users").Where(u => u.Code == i * 2).Delete();
                        Assert.IsTrue(n is 0 or 1);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            var reader = Task.Run(() =>
            {
                Parallel.For(0, 100, _ =>
                {
                    try
                    {
                        var c = ts.Query<CxUser>("users").Count();
                        Assert.IsTrue(c >= 50 && c <= 100);
                        var left = ts.Query<CxUser>("users").Where(u => u.Code % 2 == 1).ToList();
                        Assert.IsTrue(left.All(u => u.Code % 2 == 1));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            Assert.IsTrue(Task.WaitAll([writer, reader], 20000));
            ThrowIfAny(errors);
            Assert.AreEqual(50, ts.Query<CxUser>("users").Count());
            Assert.IsTrue(ts.Query<CxUser>("users").ToList().All(u => u.Code % 2 == 1));
        }

        [TestMethod]
        public void MixedInsertUpdateDeleteQueryJoin()
        {
            using var eng = Configuration.GetDbEngineForTest();
            eng.TimeoutMilliseconds = 20000;
            using var ts = eng.StartTransaction();
            Create(ts);
            SeedUsers(ts, 80);
            SeedOrders(ts, 80);

            var errors = new ConcurrentBag<Exception>();

            var insert = Task.Run(() =>
            {
                Parallel.For(80, 120, i =>
                {
                    try
                    {
                        ts.Insert("users", new CxUser { Code = i, Score = i, Active = true });
                        ts.Insert("orders", new CxOrder { UserCode = i, Amount = i });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            var update = Task.Run(() =>
            {
                Parallel.For(0, 40, i =>
                {
                    try
                    {
                        ts.UpdateEntity("users", "Code", i, new CxUser { Code = i, Score = 9000 + i, Active = true });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            var delete = Task.Run(() =>
            {
                Parallel.For(40, 50, i =>
                {
                    try
                    {
                        ts.Query<CxUser>("users").Where(u => u.Code == i).Delete();
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            var query = Task.Run(() =>
            {
                Parallel.For(0, 80, _ =>
                {
                    try
                    {
                        var c = ts.Query<CxUser>("users").Count();
                        Assert.IsTrue(c >= 70 && c <= 120, $"Count {c}");
                        ts.Query<CxUser>("users").Where(u => u.Active).Take(5).ToList();
                        ts.Query<CxUser>("users")
                            .Join<CxOrder>("orders", (u, o) => u.Code == o.UserCode)
                            .Count();
                        ts.Query<CxUser>("users")
                            .WhereExists<CxOrder>("orders", (u, o) => u.Code == o.UserCode)
                            .Count();
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
            });

            Assert.IsTrue(Task.WaitAll([insert, update, delete, query], 25000));
            ThrowIfAny(errors);

            // started 80; +40 inserts (80..119); -10 deletes (40..49) => 110
            Assert.AreEqual(110, ts.Query<CxUser>("users").Count());
            Assert.AreEqual(40, ts.Query<CxUser>("users").Where(u => u.Score >= 9000).Count());
            Assert.IsFalse(ts.Query<CxUser>("users").Where(u => u.Code == 45).Exists());
            Assert.IsTrue(ts.Query<CxUser>("users").Where(u => u.Code == 100).Exists());
            Assert.AreEqual(110, ts.Query<CxUser>("users")
                .Join<CxOrder>("orders", (u, o) => u.Code == o.UserCode)
                .Count());
        }

        [TestMethod]
        public void SharedQueryable_OtherThread_Throws_CreatorStillWorks()
        {
            using var eng = Configuration.GetDbEngineForTest();
            using var ts = eng.StartTransaction();
            Create(ts);
            SeedUsers(ts, 40);

            var q = ts.Query<CxUser>("users").Where(u => u.Code >= 0).Take(20);
            q.First();
            Assert.AreEqual(20, q.ToList().Count);

            Exception? other = null;
            var t = new Thread(() =>
            {
                try
                {
                    q.ToList();
                }
                catch (Exception ex)
                {
                    other = ex;
                }
            });
            t.Start();
            t.Join();
            Assert.IsInstanceOfType(other, typeof(LumDbEngine.Element.Exceptions.LumException));
            Assert.IsTrue(other!.Message.Contains("thread"));
            Assert.AreEqual(20, q.ToList().Count);
        }
    }
}
