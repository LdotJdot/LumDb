# LumDb 3.0.1 缺陷报告与优化计划（决策稿）

- 目标库：LumDb（C# / .NET 10，单文件嵌入式，页面格式 1.3.9，包版本 3.0.1）
- 机器：LUOJIN / Windows NT 10.0.22000.0 / .NET SDK 10.0.401 / 运行时 10.0.12
- 采集时间：2026-10-27 11:03–11:05（修复前现场，两轮完整矩阵）；12:10–13:02 为实施、扩展场景与复跑，见 **§11**
- 证据文件：`TestEvidence/defect_hunt_evidence_20261027.md`（11:03 段为修复前，12:3x 段为修复后，12:58 段为使用场景第二批的现场）；全量复跑控制台输出另存为 `TestEvidence/full_suite_run_20261027_1301.log`
- 约束：§0–§9 是"只加测试、不改引擎"阶段的现场；§10–§11 是按批准决策实施源码修改后的兼容性评估与验收

---

## 0. 结论摘要

| 类别 | 数量 | 说明 |
|---|---|---|
| S1 静默成功 / 引擎级不可用 | 3 | 失败事务仍可”成功”提交空结果 1 项；提交 IO 失败、跨线程 Dispose 各一种永久砖化路径 2 项 |
| S2 错误行为/误导 | 3 | 事务池 32 隐性上限 + 错误文案、文件头零校验、双 API 语义分裂 |
| S3 死配置 | 1 | `TransactionPolicy.Serializable` 引擎从不读取 |
| 性能瓶颈 | 3 | 冷缓存点查 3.57–5.74×、提交 ~180 tx/s、读者阻塞写者 ~13× |
| 已确认良性 | 1 | WAL 崩溃一致性（DH7，5 档强杀 ×2 轮全绿） |

基线（既有套件，排除 DefectHunt）：**310 通过 / 0 失败，耗时 1m20s**；DefectHunt 12 用例 **6 红 / 6 绿**，其中红色全部为"缺陷仍在"的证明用例，绿色为"行为固化 + 性能数值"用例。因此下列缺陷**均不在既有回归覆盖范围内**。

---

## 0.5 术语与故障路径（回答复核问题）

### "砖化"是什么

- 定义：进程内该 `DbEngine` 实例在**当前进程生命周期内**再也开不出新事务 —— 同线程 0 ms 抛 `IllegaTransaction`，其他线程等满 3 s 抛 `Transaction time out`。不是"一次操作失败"，而是**整个引擎实例报废**。
- 为什么永久：引擎级 `ReaderWriterLockSlim` 被某线程持有后没有释放，而 .NET 的 RwLock **没有强制复位 API**；`DbEngine.Dispose()` 又要先等所有事务归还（`DbEngine.cs:379-403`），所以"关掉重开"同样走不通。
- 触发门槛：DH2 证明**只要事务 Dispose 不在创建线程上**就会砖化。`using var tx = eng.StartTransaction(); await ...;` 在线程池/控制台环境里续体经常换线程，属于常规写法，不是极端用法。

### 提交路径与 IO 失败点（DH1 的现场是真实故障模型）

file 引擎一次 `SaveChanges()` 的完整路径（`DbCache.cs:68-115`）：

```
SaveChanges → WriteAction → db.SaveCurrentPageCache(dbEngine)
  ├─ DbLog.Create            // 生成 <db>.log（引擎注释称 backup log，实为提交日志）
  ├─ WriteState(Writing) + 写 header + 写全部脏页
  ├─ WriteState(Done)
  ├─ DumpToDbEngine          // 回放到主文件 + Flush(true) 落盘
  └─ dblog.Dispose()         // 删除 .log
```

DH1 的失败点是**第一步**：把 `<db>.log` 换成同名目录，`DbLog.Create` → `DbLogUtils.Create` 抛 `UnauthorizedAccessException`。等价的真实场景：

- 目录/文件 ACL 变更、服务账号失去写权限、文件被置只读；
- 磁盘满、配额满（可能落在写日志或回放主文件任一阶段）；
- 杀毒软件 / 备份代理 / 搜索索引 / 同步盘（OneDrive）在提交瞬间独占 `.log` 或主文件 —— Windows 上最常见；
- 网络盘断开、移动介质拔出、休眠唤醒后句柄失效；
- 路径被同名目录占位、路径过长、UNC 不可达。

结论：**这不等于"备份日志出问题说明数据已经坏了"，而是"这次提交没能落盘"**。数据没写进去是合理的（DH1 里失败的提交确实没落盘）；不可接受的是提交失败之后**引擎报废**，而且连"放弃这个事务"都做不到（`Discard()` 要拿同一把锁，同样抛异常）。

### 关于"哪个失败"（原失败语义问题）

指 `tx.Insert(...)` 因唯一键冲突/列校验失败而抛 `LumException: Key already existed: k` 这一类**语句级失败**。争点不是"该不该回滚事务"（你的口径：应当整体作废），而是"作废之后事务还能不能被继续使用、`SaveChanges()` 还能不能返回成功"。

---

## 1. 缺陷明细

### DEF-01（S1，静默成功）DbCell API 插入失败后，事务可”成功”提交一个空结果

- 用例：`DH4_FailedInsert_SilentlyRollsBackPriorWork_DbCellApi`（`[DataRow(Memory)]`、`[DataRow(File)]`），**红**
- 现场（两轮一致，Memory 与 File 完全相同）：

| 观测项 | 值 |
|---|---|
| 重复键异常 | `LumException: Key already existed: k` |
| 失败后同事务第二条插入是否成功 | `False` |
| 失败后同事务内首行是否可见 | `False` |
| 提交后 A 行（失败前写入）是否存在 | `False` |
| 提交后 B 行（失败后写入）是否存在 | `False` |
| 提交后表内行数 | `0` |

- 根因：`LumDbEngine/Element/Engine/Transaction/LumTransaction_Insert.cs:21-29`，`catch` 中调用 `Discard()`，而 `Discard()` 执行 `db.Reset()`（`LumTransaction.cs:113-126`），即丢弃整个事务缓存；`LumTransaction_Delete.cs` 同一模式。
- 影响（按用户复核口径拆成三点）：
  1. **"整个事务丢失"本身符合预期**（同事务内任一语句失败 → 事务整体不生效），不算缺陷。
  2. **缺陷在于没有"事务已作废"的信号**：失败后事务对象仍可继续 `Insert`/`Find`，`SaveChanges()` 返回成功，最终静默提交 0 行。调用方 `catch` 住重复键异常后继续工作，会以为后半段提交成功。
  3. **两套 API 结果相反**（见 DEF-06）：Entity 路径把 A、B 两行都提交了，等于"失败语句没有使事务作废"，与同一规则冲突。
- 证据行：`defect_hunt_evidence_20261027.md:184-198`、`:392-406`

### DEF-02（S1，引擎级不可用）提交时 IO 失败 → 引擎级写锁泄漏 → 引擎永久砖化

- 用例：`DH1_CommitIoFailure_LeaksEngineWriteLock_AndBricksEngine`，**红**
- 现场：用目录占位使 `.log` 无法创建

| 观测项 | 值 |
|---|---|
| `SaveChanges` 异常 | `UnauthorizedAccessException: Access to the path '...log' is denied.` |
| `Dispose` 异常 | 同上（Dispose 也走同一 `WriteAction` 路径，二次失败） |
| 同线程新事务 | `IllegaTransaction`（"you attempted to use an read only transaction immediately after a normal transaction"），**0 ms** |
| 跨线程新事务 | `Transaction time out`，**2999 / 3007 ms** |

- 根因：`LumDbEngine/Element/Engine/Lock/LockTransaction.cs:102-123`，`WriteAction` 在 upgradeable-read 状态下 `TryEnterWriteLock` 后直接 `act(); ExitWriteLock();`，**无 try/finally**；`act()` 抛异常（提交写文件失败）时写锁永不释放。`LumTransaction.Dispose`（`LumTransaction.cs:145-152`）的 `finally` 只释放 upgradeable-read，救不回漏掉的写锁。
- 影响：一次磁盘错误（权限、磁盘满、路径被占用、只读介质）就让**整个进程内该 `DbEngine` 实例永久不可用**：同线程立刻被重入检查拒绝，其他线程每次都要等满 3s 再失败。没有 API 可重建锁状态（`DbEngine.Dispose` 自身也依赖事务池回收）。
- 证据行：`:146-151`、`:352-359`

### DEF-03（S1，引擎级不可用）跨线程 Dispose 事务 → `SynchronizationLockException` + 引擎砖化

- 用例：`DH2_DisposeTransactionOnForeignThread_BricksEngine`，**红**
- 现场：事务在 A 线程创建，在 B 线程 Dispose

| 观测项 | 值 |
|---|---|
| `Dispose` 异常 | `SynchronizationLockException: The upgradeable lock is being released without being held.` |
| 新事务探针 | `Transaction time out`，**3007 / 3014 ms** |

- 根因：`ReaderWriterLockSlim` 有线程亲和性；`LockTransaction.Dispose()`（`LockTransaction.cs:126-149`）在非持有线程调用 `ExitUpgradeableReadLock` 直接抛异常，且**抛异常前锁已不可释放**。ASPNET/线程池场景下"创建与释放不在同一线程"（`await` 后续、`Task.Run`、DI 作用域跨线程释放）是常态，因此这是**框架级必然踩中**的路径，而非极端用法。
- 影响：同 DEF-02，引擎永久砖化；异常信息只描述锁状态，未提示"事务必须同线程释放"。
- 证据行：`:153-156`、`:361-364`

### DEF-04（S2，可用性/误导）事务池隐性上限 32，且报"引擎已释放"

- 用例：`DH3_TransactionPool_Capacity32_And_MisleadingDisposedError`，**红**
- 现场：

| 观测项 | 值 |
|---|---|
| 第 33 个并发事务异常 | `LumException: Transaction cannot be accessed beacuse the dbEngine has already be disposed early.` |
| 失败耗时 | **2998 / 3007 ms**（等满超时才失败） |
| 32 个已持有事务是否仍能正常读 | `True` |
| 引擎实际健康 | `True` |

- 根因：`DbEngine.cs:160-162` `MaxSemaphoreCount = 32` 固定；`RegisterTransaction`（`DbEngine.cs:304-333`）`Wait` 超时返回 `false` → 构造处 `LumTransaction.cs:79-82` 抛 `DbEngDisposedEarly`。另有一处读码缺陷：`RegisterTransaction` 的 `catch`（`:329-332`）把**任何**异常（包括它自己抛的 `InternalError`、DEBUG 下的 `ThrowIfTrue("dnengine")`）统一改写成 `TransactionTimeout`，调用方拿不到真实原因（读码结论，未单独实测）。
- 影响：32 是并发只读事务也很容易达到的量级（长事务 + 并发请求）；失败方式是"先阻塞 3 秒再给出误导性的已释放错误"，排障成本极高。
- 证据行：`:158-162`、`:366-370`

### DEF-05（S2，静默接受坏文件）文件魔数与版本号从不校验

- 用例：`DH8_FileMagicAndVersion_AreNeverValidated`，**红**
- 现场：

| 输入 | 结果 |
|---|---|
| 抹掉 6 字节签名 `LumDBS` | `READ OK: payload`（照常打开并读出数据） |
| `FileVersion` 改为 9.9.9 | `READ OK: payload` |
| 10 字节垃圾文件 | `EndOfStreamException: Unable to read beyond the end of the stream.`（非受控拒绝） |

- 根因：`LumDbEngine/Element/Structure/DbHeader.cs:109-124`，`Read` 只按偏移读字段，**没有** `DbID` 比对、`FileVersion` 范围校验、最小文件长度校验。头部写入处 `:95-106` 写入了 `DbID`/`FileVersion`，即这两项元数据"只写不读"。
- 影响：磁盘损坏、被截断、被其他格式文件冒名、跨版本误开时，引擎会在无任何提示的情况下继续读写，可能产生二次损坏；10 字节垃圾文件抛出的是裸 `EndOfStreamException`（非 `LumException`），调用方无法按类型识别"文件非法"。
- 证据行：`:204-209`、`:417-422`

### DEF-06（S2，语义分裂）Entity API 与 DbCell API 对同一失败的处理相反

- 用例：`DH5_EntityApi_SameFailure_KeepsPriorWork_InconsistentWithDbCellApi`，**绿（固化不一致）**
- 现场：同样的重复键失败（`Key already existed: Code`）后，Entity 路径 `A=True`、`B=True` 均正常提交；DbCell 路径（DEF-01）最终 0 行。
- 根因：`LumTransaction_LumEntity.cs:25-30`（`Insert<T>`）无任何 `catch`，与 `LumTransaction_Insert.cs:21-29` / `LumTransaction_Delete.cs:17-21` 的 `catch → Discard()` 不一致；同一文件里 `Create<T>`（`:14-22`）失败却会 `db.Reset()`，即该文件内部两种失败策略也并存。
- 影响：同一个事务里混用两套 API 时，数据留存与否取决于调用的是哪个重载；用户无法建立稳定心智模型，也无法写出可靠的错误处理。
- 证据行：`:408-411`

### DEF-07（S3，死配置）`TransactionPolicy.Serializable` 无任何实现

- 用例：`DH6_TransactionPolicy_Serializable_IsDeadConfiguration`，**绿（固化死配置）**
- 现场：`(TransactionPolicy)12345` 被公共字段原样接受；`ReadCommitted` 与 `Serializable` 观测序列完全相同（`before=1, after=2`）。
- 根因：`DbEngine.cs:68` `public TransactionPolicy TransactionPolicy = ...` 为公共字段，grep 全库仅命中定义处与一个既有测试，引擎读路径从不读取。
- 影响：用户以为设置 `Serializable` 就获得串行化隔离，实际没有；非法枚举值也不报错。属于"承诺了不存在的保证"。
- 证据行：`:200-202`、`:413-415`

---

## 2. 性能瓶颈（实测数值）

### PERF-01 每事务独立缓存 → 跨事务无共享缓冲池

- 用例：`P1_PerTransactionColdCache_PointReadCost`（20,000 行，400 次点查）
- 数据：同事务预热后 **122.3 µs/op**；每次新事务点查 **437.1 / 702.0 µs/op** → **3.57× / 5.74×**
- 建表 + 20k 行单事务写入：**1121 / 1295 ms**
- 根因：`LumTransaction.cs:66` 每事务 `new DbCache(...)`，`DbCache` 生命周期与事务绑定，跨事务无法复用已读页。
- 证据行：`:164-169`、`:372-377`

### PERF-02 提交固定成本高（.log 创建/写/回放/删除 + fsync）

- 用例：`P2_CommitThroughput_SmallAndEmptyTransactions`（200 样本）
- 数据：文件引擎单行事务 **153 / 187 tx/s**；空提交（无脏页）**223 / 336 tx/s**；内存引擎 **1429 / 2238 tx/s**
- 推算：空提交吞吐 223–336 tx/s ⇒ 每次提交固定开销约 **3.0–6.5 ms**；单行事务再叠加约 2–3 ms 数据成本。
- 影响：任何"每条记录一个事务"的常规用法（日志、采集、逐条导入）都被压在 200 tx/s 以下；内存引擎高一个数量级，说明瓶颈在文件提交路径而非事务框架。
- 证据行：`:171-175`、`:379-383`

### PERF-03 只读事务整体阻塞写提交（无快照/MVCC）

- 用例：`P3_ReaderTransaction_BlocksWriterCommit`
- 有效采样：无读者提交 **13.6 ms** → 读者持锁扫描 195 ms 期间提交 **181.3 ms**，约 **13×**
- 无效采样（如实保留）：两轮中读者扫描窗口仅 11/19 ms，与提交窗口未重叠，测得 9.6/10.6 ms，不能作为结论。
- 根因：读事务持 `ReaderWriterLockSlim` 读锁，`SaveChanges` 需写锁（`LumTransaction.cs:93-95`），二者互斥且无快照隔离。
- 证据行：`:177-182`、`:424-429`

---

## 3. 已确认良性 / 不作为缺陷

- **DH7 崩溃一致性可信**：5 档强杀（200/500/900/1500/2200 ms）×2 轮全部 `OK`；重开后行数均落在 `[lastCommitted+1, lastCommitted+2]`，载荷逐行一致，无空洞、无错位。WAL 回放在"进程被强杀"这一最常见故障模型下是正确的。
- **基线无回归**：310/310 通过，新增用例未污染既有行为。
- **代码中已带 `BUG-03/06/10/14` 修复注释**：说明部分历史缺陷已修（如 `Dispose` 的 try/finally、`DbCache` 构造失败释放锁、`disposed` 的 Volatile 化），本轮红用例是**这些修复之外仍然存在的洞**（尤其 `LockTransaction.WriteAction` 本身缺 try/finally，导致 BUG-03 的 finally 也救不回来）。

---

## 4. 优化计划（分四批，按"止血 → 正确性 → 性能 → 工程化"）

> 工作量为人日估算；每项都给出"验收 = 哪个红用例转绿"。

### 批次 P0：止血（建议 2–3 人日，改动集中在锁与事务池）

**O1 修 `WriteAction` 的锁泄漏（对应 DEF-02）**
- 改动：`LockTransaction.cs:102-123` 改为 `try { act(); } finally { ExitWriteLock(); }`；同时把"是否已进入写锁"记录进状态机，使 `Dispose()` 幂等（重复 Dispose 不抛 `SynchronizationLockException`）。
- 风险：低。仅影响异常路径。
- 验收：DH1 转绿（提交 IO 失败后新事务可正常创建；同线程不再 0 ms 报 `IllegaTransaction`）。
- 备注：此项**不能**修好 DEF-03（线程亲和），但可把"一次磁盘错误毁掉整个引擎"降到"一次操作失败"。

**O2 锁模型改为线程无关（对应 DEF-03）**
- 方案 A（仅约束）：记录 owner `ManagedThreadId`，跨线程 Dispose 立刻抛明确异常（"transaction must be disposed on the thread that created it"）。**代价：仍然泄漏引擎级读锁 → 仍然砖化**，只是报错更清楚。不推荐单独采用。
- 方案 B（推荐）：用 `SemaphoreSlim` + 读计数/写互斥自建 RW gate，或引入线程无关的 async RW 锁；重入检测自行维护（同线程二次开事务仍立即抛 `IllegaTransaction`，保持既有测试语义）。
- 改动面：`LockTransaction.cs` 全部、`LumTransaction.cs:52-58/93/101/108/118/147-152`、`DbEngine.ReadWriteLock` 的 5–6 个使用点、`DbEngine_Vacuum.cs:26`、`DbEngine.Dispose`。
- 风险：中高（并发核心）。需要保留 `IsWriteLockHeld` 类语义的替代实现，并跑满 310 基线 + 并发压测。
- 验收：DH2 转绿（跨线程 Dispose 不再抛 `SynchronizationLockException`，新事务可立即创建）；基线 310 全绿。

**O3 事务池去掉 32 硬上限 + 修正异常语义（对应 DEF-04）**
- 改动：`MaxSemaphoreCount` 改为可配置 `MaxConcurrentTransactions`（`0` = 不限制，默认不限制或给足 1024）；池满时抛**专属**异常（如 `TransactionPoolBusy`），不再复用 `DbEngDisposedEarly`；修掉 `RegisterTransaction` 的 `catch` 把 `InternalError` 改写成 `TransactionTimeout` 的问题（`DbEngine.cs:329-332`）。
- 风险：低–中（需确认无隐藏的"32 即资源上限"假设，例如锁与缓存的内存占用）。
- 验收：DH3 转绿（第 33、第 1000 个事务可创建；若配置上限则立刻失败且错误文案准确）。

### 批次 P1：正确性语义（建议 3–5 人日）

**O4 统一"失败语句"的语义（对应 DEF-01 / DEF-06）—— 语义已定（D2）**
- 规则：任意语句（`Insert/Delete/Update/Alter`）抛异常 ⇒ 整个事务标记 `Faulted`，工作集作废、永不落库（符合"一个语句失败就该整事务丢失"的口径），**不再讨论语句级 undo**。
- 必须补齐的是"信号"和"一致性"：
  1. 失败后同事务的后续操作**立即抛** `LumException(TransactionAborted)`（当前仍可继续用，甚至返回 `IsSuccess=False` 而不抛）；
  2. `SaveChanges()` 在 faulted 状态抛 `TransactionAborted`（当前"成功提交 0 行"）；
  3. `Dispose()` 静默回滚、不再二次写盘（当前 DH1 的 Dispose 会重试提交并二次抛异常）；
  4. Entity API 与 DbCell API 共用同一状态机（当前 Entity 路径把失败事务的数据提交了）。
- 改动：`Discard()` 从"catch 里顺手调用"改为显式状态迁移 `Active → Faulted`；`LumTransaction_LumEntity.cs` 的 `Insert<T>`/`Create<T>` 接入同一路径。
- 验收：DH4 断言改为"失败后第二条 Insert 抛 `TransactionAborted`、`SaveChanges` 抛 `TransactionAborted`、提交后 0 行"；DH5 改为"Entity 路径同样 0 行且抛异常"；新增"faulted 事务 Dispose 不落盘"用例。

**O5 文件头校验与受控拒绝（对应 DEF-05）**
- 改动：`DbHeader.Read` 校验 ① 文件长度 ≥ `HEADER_SIZE`(100)；② `DbID == "LumDBS"`；③ `FileVersion` 落在支持集内；不满足抛 `LumException`（新增 `InvalidDatabaseFile` / `UnsupportedFileVersion`），并把 `EndOfStreamException` 包装掉。
- 版本管理：见 **§8《文件格式版本识别与兼容策略 V1》**（不改页面布局，先加校验；支持集当前锁死 1.3.9）。
- 验收：DH8 两条"READ OK"变为"抛受控异常且消息含签名/版本诊断"；10 字节/99 字节垃圾文件抛 `LumException` 而非 `EndOfStreamException`。

**O6 处理 `TransactionPolicy` 死配置（对应 DEF-07）**
- 方案 A：删除枚举与字段（破坏性小，诚实）。
- 方案 B：保留 `ReadCommitted`，`Serializable` 在开启事务时抛 `NotSupportedException`，非法枚举值抛 `ArgumentOutOfRangeException`。
- 方案 C：真正实现串行化（全局写串行 + 读快照校验），成本接近 P2 的 MVCC 改造。
- 验收：DH6 由"固化死配置"改写为"非法值被拒绝 / Serializable 有明确行为"。

### 批次 P2：性能（建议 6–10 人日，风险最高，可与 P1 并行评估）

**O7 引擎级共享页缓存（PERF-01）**
- 设计：`DbCache` 拆为"引擎共享只读页池（LRU + pin 计数）"+"事务私有脏页覆盖层"；提交时合并覆盖层。
- 预期：冷/热倍率从 3.57–5.74× 收敛到 ≤1.5×；点查绝对值目标 ≤200 µs/op（20k 行）。
- 风险：高（页生命周期、Dispose 顺序、Vacuum/Alter 与缓存的交互）。建议先只对**只读事务**共享，再扩到写事务。

**O8 提交路径合并 / 组提交（PERF-02）**
- 设计：把"创建 `.log` → 写 → fsync → 回放 → 删除"改为可复用的日志文件 + 追加式记录，并支持 `DurabilityMode.GroupCommit`（1–5 ms 窗口内合并多次提交的 fsync）。
- 预期：单行事务从 153–187 tx/s 提到 ≥800 tx/s；空提交 ≥2000 tx/s。
- 风险：中（崩溃一致性直接受影响）。**必须**重跑 DH7 强杀矩阵，并把档位从 5 档扩到 ≥20 档、轮次 ≥3。

**O9 读写解耦（PERF-03）**
- 设计：读事务基于**提交时快照**（page-version 或 copy-on-write 根指针），写提交不再需要等待读者退出；备选低成本方案：把 `SaveChanges` 的落盘阶段移出写锁（先在锁内取页面快照，锁外写文件），可把 13× 降到 ~1.2×，但需保证快照期间无并发写。
- 预期：读者持锁期间的提交耗时从 181.3 ms 降到接近 13.6 ms 基线（≤2×）。
- 风险：高。

### 批次 P3：工程化（建议 1–2 人日）

- CrashProbe 纳入 `LumDb.sln` 或在测试工程加构建后复制步骤，去掉 DH7 对手工 `dotnet build` 的依赖。
- DefectHunt 用例加 trait 分类（`Category=Defect` / `Perf` / `Contract`），CI 默认只跑"应当全绿"的子集；已知红用例单列，修复后转绿即视为验收。
- 补测未覆盖区：`Vacuum` 与 `Alter` 在高并发/失败注入下的正确性（本轮未做专项压测）；`DbEngine.Dispose` 在砖化状态下的行为（当前用例 `finally` 吞掉了异常，属证据缺口）。
- 新增长稳测试：连续 10 万次小事务 + 周期性 Vacuum，观察文件大小与吞吐衰减。

---

## 5. 决策台账（2026-10-27 复核后）

| # | 决策点 | 状态 | 结论 / 待确认 |
|---|---|---|---|
| D1 | 修复顺序 | 待确认 | 建议：P0（DEF-02/03 锁 + DEF-01/06 事务作废信号 + DEF-04 文案）→ P1（DEF-05 头校验/版本）→ P2 性能 |
| D2 | 失败语句语义 | **已定（用户）** | 语句失败 ⇒ 整个事务作废、不落库。落地要求：事务标记 faulted，后续操作立即抛 `TransactionAborted`，`SaveChanges()` 抛异常，`Dispose()` 静默回滚；Entity 路径同规则（DEF-06 一并修）。语义不再讨论，缺的是"信号"和"两套 API 一致" |
| D3 | 事务池上限 | **已定（用户）** | 保留 32；新增 `TooManyOpenTransactions`（消息写清"上限 32，已有 32 个未释放"），去掉 `RegisterTransaction` 把 `InternalError` 改写成 `TransactionTimeout` 的 catch（`DbEngine.cs:329-332`）；建议池满**立即失败**而非等 3 s，必要时留 `WaitOnPoolFull` 开关 |
| D4 | 锁模型 | **待确认**（见下） | 推荐 B |
| D5 | 坏文件/跨版本 | **待确认** | 见 §8《文件格式版本识别与兼容策略 V1》；默认严格校验，当前支持集仅 1.3.9 |
| D6 | `TransactionPolicy` | 待确认 | 建议保留 `ReadCommitted`；非法值抛 `ArgumentOutOfRangeException`；`Serializable` 抛 `NotSupportedException`（诚实、低成本） |

### D4 的三个选项（"砖化"根因决定选型）

| 选项 | 做法 | 能否消除砖化 | 风险 / 工作量 |
|---|---|---|---|
| A | 只给 `WriteAction` 加 try/finally | 只修 DEF-02；DEF-03 仍砖化 | 极小（0.5 人日） |
| B（推荐） | 引擎级锁换成线程无关 RW gate（`SemaphoreSlim` + 读计数/写互斥），保持"事务级持锁"语义与现有 API | **能**（任何线程都能释放） | 中（2–3 人日，改动 6–8 处，跑满 310 基线） |
| C | B + 把持锁粒度从"事务生命周期"降到"单次操作 + 提交校验" | 能，且顺带解 PERF-03 | 高（与 P2 共享缓存一起做） |

关键事实：`ReaderWriterLockSlim` 没有强制复位能力，所以**选项 A 无法真正修复 DEF-03**，只能把报错变清楚；要让"引擎不再报废"必须选 B 或 C。

---

## 6. 验收门槛（修复后必须满足）

1. 基线套件 `--filter "FullyQualifiedName!~DefectHunt"` 仍为 **310/310**，耗时不得劣化 >20%。
2. 红色用例转绿：DH1、DH2、DH3、DH4(Memory)、DH4(File)、DH8（DH8 需按 O5 改写断言为"受控拒绝"）。
3. 绿色固化用例保持语义：DH5 与 DH4 结果一致；DH6 改为"有明确行为"；DH7 强杀矩阵 ≥20 档全绿。
4. 性能门槛：P1 冷/热 ≤1.5×；P2 单行事务 ≥800 tx/s；P3 读扫描期间提交 ≤2× 基线。
5. 新增不变量用例（修复时同步添加）：`EngineUsableAfterCommitFailure`、`ForeignThreadDisposeContract`、`PoolBeyond32`、`FailedStatementIsolation`、`InvalidHeaderRejected`。

## 7. 复现命令

```
dotnet build UnitTestLumDb\UnitTestLumDb.csproj -c Debug
dotnet test  UnitTestLumDb\UnitTestLumDb.csproj -c Debug --filter "FullyQualifiedName~DefectHunt"
dotnet test  UnitTestLumDb\UnitTestLumDb.csproj -c Debug --filter "FullyQualifiedName!~DefectHunt"
```

DH7 需要 `CrashProbe` 可执行文件（当前 `DhSupport.FindCrashProbe` 会在工作区内查找；未纳入 sln 时需先 `dotnet build CrashProbe\CrashProbe.csproj -c Debug`）。

---

## 8. 附录：文件格式版本识别与兼容策略 V1（回答 D5）

### 8.1 现状（读码结论）

| 事实 | 位置 |
|---|---|
| 6 字节签名 `LumDBS`（`IDLENGTH = 6`） | `DbHeader.cs:22-24` |
| 页面格式版本常量 `1_003_009`（= 1.3.9） | `DbHeader.cs:26-31` |
| 已有 `FormatVersion()`，可把打包值打印成 "1.3.9" | `DbHeader.cs:34-35` |
| 头部 100 字节，`STATE_POS = 88`，`96..99` 为 `LOCKER_POS`（注释 "not in use"） | `DbHeader.cs:12-18` |
| `WriteBytes` 写入 `DbID` 与 `FileVersion` | `DbHeader.cs:95-106` |
| `Read` 只按偏移读，**不比对、不校验** | `DbHeader.cs:109-124` |
| 全库无版本表、无迁移器、无"支持哪些版本"的声明 | 读码确认 |

即：签名与版本号是"只写不读"的元数据，所以 DH8 里抹签名、改版本都能照常打开。

### 8.2 提案（不改页面布局，先加校验，零兼容风险）

1. **声明支持集**：`SupportedFormats = { 1_003_009 }`（当前唯一可读写版本），常量放在 `DbHeader`，并暴露只读属性 `DbEngine.SupportedFileVersions`（诊断用）。
2. **打开校验顺序**：文件长度 ≥ 100 → `DbID` 与 `FileID` 逐字节相等 → `FileVersion` ∈ 支持集 → 才继续读页。
3. **失败语义**：新增两条 `LumException` 消息 —— `InvalidDatabaseFile`（长度不足/签名不符）、`UnsupportedFileVersion`（消息用 `FormatVersion()` 打印"文件为 x.y.z，本组件支持 …"）；`EndOfStreamException` 一律包装成前者。
4. **兼容规则（为将来预留，当前不启用）**：
   - `FileVersion < 当前`：走升级器链 `IFileUpgrader(v→v+1)`；默认**关闭**（`OpenOptions.AllowUpgrade = false`），关闭时抛 `UnsupportedFileVersion` 并提示"可显式允许升级"。
   - `FileVersion > 当前`：直接拒绝（"file created by a newer LumDb"），绝不尝试读取。
5. **不改布局的扩展位**：需要新增特性标记时用 `LOCKER_POS`（96–99）写 `FormatFeatures` 位，不动现有字段偏移，旧版本读到即忽略。
6. **验收矩阵**（新增参数化用例）：签名正确 / 签名篡改 / 1.3.8 / 1.3.9 / 1.4.0 / 9.9.9 / 空文件 / 10 字节 / 99 字节 / 恰好 100 字节 → 逐项断言"接受"或"受控拒绝 + 正确消息"。

### 8.3 需要你确认的前置信息

- 是否存在 **1.3.9 之前**的历史数据库文件需要继续打开？若有，请给样本（或最低版本号），我把它们加入支持集或补升级器；若没有，支持集就锁死 1.3.9。
- 是否需要"只读打开旧版本"（不升级、不允许写）这条路径？这会决定 `OpenOptions` 的形状。

---

## 9. 附录：P0 四项的范围界定（避免混淆）

### 9.1 先分清"失败发生在哪一层"

| 失败位置 | 具体例子 | 当前现象 | 归属 |
|---|---|---|---|
| 语句执行（未落盘） | 重复键 `Key already existed: k`、列校验失败、类型不匹配 | 事务被静默 Reset，随后"成功提交 0 行"；Entity 路径反而全部提交 | **P0-d**（DEF-01/06） |
| 提交落盘 | `DbLog.Create` 失败、写日志失败、回放主文件失败、fsync 失败 | `SaveChanges` 抛异常后**写锁泄漏 → 引擎报废**，`Dispose` 二次抛异常 | **P0-a**（DEF-02） |
| 跨线程释放 | 在非创建线程 `Dispose()` | `SynchronizationLockException` → 引擎报废 | **P0-b**（DEF-03） |
| 事务创建 | 第 33 个并发事务 | 等 3 s → 抛"引擎已释放"（实际健康） | **P0-c**（DEF-04） |

所以 **P0-a 不是修 Insert**，它修的是"提交时抛异常会把引擎弄死"；Insert 的问题在 **P0-d**。

### 9.2 P0-a：`WriteAction` 的异常安全（DEF-02）

```csharp
// 现在（LockTransaction.cs:106-117）
if (isWrite == State.UpgradeableRead) {
    if (rw.TryEnterWriteLock(timeout)) { act(); rw.ExitWriteLock(); }   // act 抛异常 → 锁泄漏
    else throw LumException.Raise(TransactionTimeout);
}

// 改后
if (isWrite == State.UpgradeableRead) {
    if (!rw.TryEnterWriteLock(timeout)) throw LumException.Raise(TransactionTimeout);
    try { act(); } finally { rw.ExitWriteLock(); }
}
```

同时让 `LockTransaction.Dispose()` 幂等（重复 Dispose 不再 `Exit*` 未持有的锁）。效果：提交失败只让**本次提交**失败，引擎继续可用；DH1 转绿。`0.5d`。

### 9.3 P0-b：把引擎级锁从"线程亲和"改成"线程无关"（DEF-03）

现状：事务从构造那一刻就持有引擎级 **upgradeable read** 锁（`LumTransaction.cs:58`），直到 `Dispose()` 才 `ExitUpgradeableReadLock`；`ReaderWriterLockSlim` 的 `Exit*` **必须由持有线程调用**，异线程调用直接抛 `SynchronizationLockException`，且没有任何 API 能强制释放 → 引擎报废。

改法：用全局计数实现等价的 RW gate，**状态在引擎上，不在线程上**，任何线程都能释放：

- `SemaphoreSlim writeLock(1,1)`：写互斥（`SaveChanges`/`Discard`/`Vacuum`）；
- `SemaphoreSlim upgradeableLock(1,1)`：同一时刻只允许**一个** upgradeable 持有者 —— 这条必须保留，否则两个写事务同时"读锁升级写锁"会互相等死；
- `int readerCount` + `SemaphoreSlim readersZero`：普通读并发，写者进入前等读者清零，并置 `writerPending` 阻止新读者插队（写者优先，避免饿死）。

必须一起搬过去的既有语义：

1. **同线程重复开事务** 目前靠 `LockRecursionException` + `IsUpgradeableReadLockHeld`（`LumTransaction.cs:52-53`）免费得到 `IllegaTransaction`；gate 没有线程亲和后，要自己维护"每线程事务计数"（`[ThreadStatic]` 或 `AsyncLocal`）才能保持这个行为；
2. `Vacuum` 的"当前线程已持有事务则拒绝"（`DbEngine_Vacuum.cs:26`）要用同一份线程计数；
3. 3 s 超时语义与 `DbEngine.Dispose` 等全部事务归还（`DbEngine.cs:379`）要一一对应。

收益：DH2 转绿（异线程 Dispose 只是归还一个计数，不再爆炸）。注意：**它不改善性能**，读仍阻塞写（PERF-03 仍留给 P2 的 C 方案）。`2–3d`，风险中：需要并发压测（多读多写 + 超时 + 异线程）。

### 9.4 P0-c：保留 32，但把"错"改成"对"（DEF-04）

按你的决定不做扩容，只做四件事：

1. 新增异常 `TooManyOpenTransactions`，消息写清"并发事务上限 32，当前 32 个未释放"，不再复用 `DbEngDisposedEarly`；
2. **池满立即失败**（`Wait(0)`，或可配 `PoolWaitMilliseconds` 默认 0），不再空等 3 s；
3. 修 `RegisterTransaction` 的 `catch`：现在它把 `InternalError`、DEBUG 断言异常统统改写成 `TransactionTimeout`（`DbEngine.cs:329-332`），要只捕获真正的等待失败；
4. 把 32 变成显式常量/只读属性（`MaxConcurrentTransactions = 32`）并写进 XML 文档，避免再被当成隐藏行为。

验收：DH3 改为"第 33 个事务在 <50ms 内抛 `TooManyOpenTransactions` 且消息含 32；释放一个后可正常开新事务"。`0.5d`。

### 9.5 P0-d：事务 `Faulted` 状态机 + 两套 API 合流（DEF-01/06）

语义（已定）：任一语句抛异常 ⇒ 整个事务作废、永不落库。要补的是**信号**：

1. `LumTransaction` 增加 `Faulted` 状态（与现有 `disposed` 同样用 `Volatile` 读写）；
2. `Insert/Delete/Update/Alter/Create` 的 `catch` 统一改为 `MarkFaulted(ex)` → `Discard()` → `throw;`（保留原始异常，调用方拿到的仍是"重复键"而不是"事务已作废"）；
3. faulted 之后：任何语句或 `SaveChanges()` 立即抛 `TransactionAborted`（消息带上首次失败原因）；`Dispose()` 只释放锁 + 注销事务，**不再尝试落盘**；
4. `LumTransaction_LumEntity.cs` 的 `Insert<T>`（现在完全没有 catch）与 `Create<T>`（现在用 `db.Reset()`）接入同一状态机，删掉各自的临时处理；
5. **提交失败不进 faulted**（用户决策 2026-10-27）：磁盘错误后事务保持 `Active`、脏页保留，允许再次 `SaveChanges()` 重试；`Dispose()` 仍会做最后一次落盘，失败则原样抛异常（锁已由 P0-a 保证释放，引擎存活）。重试安全性依赖提交路径幂等：`DbLog.Create` 每次先 `PrepareForWrite` 处理残留日志（`DbLog.cs:62-91`），需补"提交失败 → 磁盘恢复 → 重试成功"的用例锁定该行为。

验收：DH4/DH5 改写断言 —— 失败后第二条 Insert 抛 `TransactionAborted`、`SaveChanges` 抛 `TransactionAborted`、提交后 0 行、Entity 路径同样 0 行；新增"faulted 事务 Dispose 不产生 `.log`、不写主文件"。`1–1.5d`，风险中（对调用方是可见的语义变化，需要 release note）。

---

## 10. 附录：兼容性评估（老代码 / 老数据库）

### 10.1 源码与 API

| 变更 | 是否改签名 | 老代码是否需要改 |
|---|---|---|
| P0-a `WriteAction` 加 try/finally | 否 | **不需要**（失败路径不再报废引擎） |
| P0-b 引擎锁换线程无关 gate | 同时把 `public ReaderWriterLockSlim ReadWriteLock`（`DbEngine.cs:61`）改为 `internal` | **不需要**：7 处引用全部在引擎内部（`DbEngine.cs:234/385`、`DbEngine_Vacuum.cs:26/260-261`、`LumTransaction.cs:52/58`、`TransactionReadonly.cs:25`）；测试程序集已有 `InternalsVisibleTo`；外部引用属未文档化用法 |
| P0-c 池满改抛新消息 | 否（沿用以 `LumException` 为基类，`catch (LumException)` 继续有效） | **不需要**；只有按消息文本匹配 `DbEngDisposedEarly` 的代码受影响 |
| P0-d 事务 `Faulted` 状态 | 否 | 依赖"失败后继续用同一事务"的代码会开始收到 `TransactionAborted` —— **有意**的行为修正 |
| P1 文件头校验 | 否 | 见 10.2 |

结论：老代码（正常用法）**不用改**；唯一的公开面变化是 `DbEngine.ReadWriteLock` 降为 `internal`（理由见 10.4）。

### 10.2 数据库文件

- **1.3.9 文件：完全兼容**。页面布局不变，只是打开时多读一次头部做校验，读写路径零改动。
- **1.3.9 之前的文件：需要单独决策**。git 历史显示页面格式版本被反复提升：`1_000_006 / 1_000_009 / 1_001_001 … 1_003_008 / 1_003_009`（1.3.9 由提交 `2944aee dev_1_3_9` 引入）。而当前代码**从不读取** `FileVersion`，即今天的实现对任何旧文件都是"按 1.3.9 布局硬读"——可能已经读错，只是没有报错。
- 加校验后这些旧文件会从"能打开"变成"受控拒绝"。三种处置：

  1. **默认拒绝 + 明确诊断 + 离线迁移**（推荐）：错误消息给出"文件为 x.y.z，本组件支持 1.3.9"；迁移用对应旧版本包把数据导出为新文件（`SaveAs` 已有路径）。
  2. **兼容开关硬读**：保留现状（有读错风险），仅加告警。
  3. **写 20+ 个版本的升级器**：成本极高，不建议。

### 10.3 行为变化清单（进 release note）

| # | 旧行为 | 新行为 | 影响 |
|---|---|---|---|
| 1 | 提交 IO 失败后引擎报废 | 只本次提交失败，脏页保留，可重试 `SaveChanges()`（DH1b 锁定） | 纯改善 |
| 2 | 跨线程 Dispose 抛 `SynchronizationLockException` 且引擎报废 | 锁正常归还，并按 `Dispose` 语义提交；不抛额外异常。需要"只回滚"时显式 `Discard()` | 改善（async 友好） |
| 3 | 第 33 个事务等 3 s 抛"引擎已释放" | 立即抛 `TooManyOpenTransactions`（消息含上限 32） | 错误语义修正 |
| 4 | 语句失败后可继续用事务，`SaveChanges()` 静默提交 0 行 | 后续操作/提交抛 `TransactionAborted` | 有意修正，需 release note |
| 5 | 坏文件/异版本文件照常打开 | 受控拒绝（`InvalidDatabaseFile` / `UnsupportedFileVersion`） | 需按 10.2 选策略 |
| 6 | 提交后新事务可能仍读到旧页（读句柄 4 KiB 缓冲未失效，见 DEF-08） | 提交后新事务必读到新页；页缓存只由 `DbCache` 负责 | 数据可见性缺陷修正 |
| 7 | 无脏页的 `SaveChanges`/`Dispose` 也会创建 `.log`，日志路径异常即报错（DEF-09） | 无改动不再触碰 `.log` | 纯改善 |
| 8 | 回放失败仍删除 `.log`（唯一完整副本），主文件被写坏后无法恢复（DEF-10） | 回放失败保留 `.log`，下次打开先重放再使用 | 耐久性改善 |
| 9 | 读者事务存活时，Dispose 一个未改动的事务也要等写租约（3 s 超时，DEF-11） | 无待提交内容不升级写租约，立即释放 | 改善 |

### 10.4 `DbEngine.ReadWriteLock` 没有必要 public（决策：改 `internal`）

事实：

- 7 处引用**全部在引擎程序集内部**：`DbEngine.cs:234`（SaveAs 快照）、`DbEngine.cs:385`（Dispose）、`DbEngine_Vacuum.cs:26`（"当前线程已持有事务"检查）、`DbEngine_Vacuum.cs:260-261`（Vacuum 取锁）、`LumTransaction.cs:52`（重入检查）、`LumTransaction.cs:58`（事务取锁）、`TransactionReadonly.cs:25`（只读事务取锁）。
- 测试/工具程序集已通过 `Assembly.cs:3-8` 的 `InternalsVisibleTo` 获得内部可见性，改成 `internal` 不影响任何测试。
- 它没有 XML 文档、没有语义承诺，类型是 `ReaderWriterLockSlim` —— 外部拿到它只能做一件事：**和引擎内部抢同一把锁**，轻则死锁、重则绕过不变量。

结论：**没有 public 的必要，改 `internal`**。而且这个选择不是"可选的破坏性变更"：P0-b 换锁后该成员的类型/语义必然变化，唯一能保持 public 的办法是留一个"不参与真实同步"的占位属性——那比删掉更危险，所以不保留转发属性。

外部影响的处置：release note 注明"`DbEngine.ReadWriteLock` 已移除（实现细节）"；若确有外部使用者，给一行替代说明（用 `StartTransaction/StartTransactionReadonly` 表达并发意图，而不是手工加锁）。

---

## 11. 实施记录与验收（2026-10-27 12:10–13:02）

### 11.1 落地清单

| # | 文件 | 改动 | 对应缺陷 |
|---|---|---|---|
| 1 | `Element/Engine/Lock/EngineGate.cs` | 新增线程无关 RW gate（读计数 + 写互斥 + upgradeable 单占 + 写者优先）；`UpgradeToWrite` 置 `writerWaiting`，避免读者流饿死提交 | DEF-02/03、PERF-03 的语义基础 |
| 2 | `Element/Engine/Lock/LockTransaction.cs` | 租约状态机（`Read/Write/UpgradeableRead`）、`Dispose` 幂等、升级写带上限（不再 `-1` 无限等待）、`WriteAction` 用 try/finally 归还写锁 | DEF-02 |
| 3 | `Element/Engine/Transaction/LumTransaction.cs` | `OwnerThreadId`；`faulted/faultReason/IsFaulted/CheckTransactionState/MarkFaulted`；`Discard()` 清除 faulted 并重载已提交镜像；`Dispose` 在 faulted/无待提交时**不落盘、不升级写租约** | DEF-01/06、DEF-11 |
| 4 | `LumTransaction_{Insert,Delete,Table,LumEntity,Alter}.cs` | 语句层统一 `catch (Exception ex) { MarkFaulted(ex); throw; }`（Entity 的 Insert/UpdateEntity/Update 原先无 catch） | DEF-01/06 |
| 5 | `Element/Engine/DbEngine.cs` | `TransactionPolicy` 改属性并校验；`StartTransaction`/`StartTransactionReadonly` 对 `Serializable` 抛 `NotSupportedException`；`MaxConcurrentTransactions = 32` 常量 + `TransactionPoolWaitMilliseconds`；`RegisterTransaction` 返回 void、先查 disposed、池满抛 `TooManyOpenTransactions`；`EnsureNoTransactionOnCurrentThread()`；打开时先 `DbHeader.ValidateImage` 再恢复日志；`Dispose` 用 `Gate?.Dispose()` | DEF-03/04/05/07 |
| 6 | `Element/Structure/DbHeader.cs` | `Read` 校验长度 ≥ 100、6 字节签名 `LumDBS`、`FileVersion == 1_003_009`，消息含实际长度/双版本；新增 `ValidateImage(IOFactory)` | DEF-05 |
| 7 | `Transaction/AsNoTracking/TransactionReadonly.cs` | 重写构造/释放：`RegisterTransaction` 后取读租约、`OwnerThreadId`、异常路径释放租约并注销 | DEF-03/04 |
| 8 | `Element/Engine/DbEngine_Vacuum.cs` | 改用 `Gate.HoldsWriterOnCurrentThread / HoldsUpgradeableOnCurrentThread` 与 `TryStart*` | DEF-03 |
| 9 | `IO/BinaryReaderPool.cs` | 读者池 FileStream 改为**无缓冲**（`bufferSize: 1`） | **DEF-08（本轮新发现）** |
| 10 | `Element/Engine/Cache/DbCache.cs` | 无脏页且 header 不脏时提交直接返回；新增 `HasPendingChanges` | **DEF-09（新）/ DEF-11（新）** |
| 11 | `Element/LogStructure/DbLog.cs` | `Close(deleteLog)`：回放失败保留 `.log` | **DEF-10（新）** |

### 11.2 实施过程中新定位的缺陷（DEF-08 ~ DEF-11）

**DEF-08（S1，数据可见性）读句柄缓冲未失效，提交后仍读到旧页**

- 现场：`Open_WhenStateByteWritingAndLogMissing_StillReadsIntactPages` 中磁盘状态字节已是 `Done(2)`（`MarkDbState` + `Flush(true)` 成功），但 `DbCache` 构造读回 `Writing(3)`；用例给出 `文件状态字节=2，内存 header.State=3`。
- 根因：`IOFactory` 的读者池是 5 个独立 `FileStream`（默认 4 KiB 缓冲），写者走另一个句柄。.NET 的 `FileStream.Seek` 落在当前已缓冲区间时直接复用缓冲，不重新读盘 —— 于是"先读页 → 提交改页 → 再读"会稳定命中旧字节。这不是测试假象：任何"事务 A 读过第 N 页、事务 B 提交改写第 N 页、事务 A′ 再读"的调用序列都会读到旧值。
- 修复：读者池无缓冲（一页一次 read syscall），页级缓存仍由 `DbCache` 负责。
- 锁定用例：`TransactionUsageScenarioTests.Scenario_CommittedUpdate_IsVisibleToNewReaders`（Memory/File，5 轮提交逐轮校验）、`Open_WhenStateByteWritingAndLogMissing_StillReadsIntactPages`。

**DEF-09（S2）空提交也会创建 `.log`**

- 现场：DH1 的"提交失败后同线程新事务"探针只做了一次读，却因 `Dispose → SaveCurrentPageCache → DbLog.Create` 抛 `UnauthorizedAccessException` 而失败 —— 即"没有改动的事务"也会碰日志路径。
- 修复：`SaveCurrentPageCache` 先算脏页/header，无改动直接返回（内存后端本来就这样短路，现在两端一致）。
- 顺带收益：`P2` 的空提交吞吐不再为 0 脏页付一次"建日志 + fsync + 删除"。

**DEF-10（S1，耐久性）回放失败仍删除 `.log`**

- 现场（读码 + DH1 的失败路径）：`DbCache.SaveCurrentPageCache` 的 `finally` 调 `dblog.Dispose()`，其内部 `DbLogUtils.Delete()` 无条件删日志。若 `DumpToDbEngine` 在回放中途失败（磁盘满 / 句柄失效），主文件已被写坏，而唯一完整副本（`.log`）被删掉；主文件的状态字节仍是 `Done`，下次打开会直接按"完好"读取坏页。
- 修复：`DbLog.Close(bool deleteLog)`；只有回放成功才删日志。失败后下次打开走 `DbLog.PrepareForWrite` 的 `Done` 分支重放（重放是幂等的），把坏页覆盖回完整镜像。

**DEF-11（S2）未改动事务的 `Dispose` 也要拿写租约**

- 现场：DH3 归还槽位后的探针 `using var ok = eng.StartTransaction()` 在 31 个读者事务存活时，`Dispose → WriteAction → UpgradeToWrite` 需要读者清零 → 3 s 后 `Transaction time out`。
- 修复：`DbCache.HasPendingChanges`；无待提交内容时只 `PagesClear` + 标记 disposed，不升级写租约。

### 11.3 最终行为契约（已由用例固化）

1. `SaveChanges` / `Dispose` = 提交；`Discard` = 回滚到已提交镜像并恢复事务可用（README 原文，未改语义）。
2. 语句抛异常 ⇒ 事务 `Faulted`：后续任何语句/`SaveChanges` 抛 `TransactionAborted`（消息带首次失败原因）；`Dispose` 不落盘；`Discard()` 可复用。
3. 提交 IO 失败 ⇒ **不**进 `Faulted`：脏页保留，磁盘恢复后同一事务可重试 `SaveChanges()`；`Dispose` 会再试一次，失败照抛，但租约一定归还。
4. 读者事务可嵌套；同线程持有任何活事务时不允许再开写事务（`IllegaTransaction`，保持既有语义）。
5. 事务池上限 32，池满默认立即失败（`TooManyOpenTransactions`，消息含上限与当前数）；`TransactionPoolWaitMilliseconds > 0` 时按该窗口等待后失败。
6. `Serializable` 在启动事务（写/只读）时抛 `NotSupportedException`；非法枚举值在赋值时抛 `ArgumentOutOfRangeException`。
7. 打开文件先校验长度/签名/版本；`Writing` 且无 `.log` 时把状态字节修回 `Done` 并继续读原页。

### 11.4 验收结果

- 编译：`dotnet build LumDbEngine\LumDbEngine.csproj -c Release` → **355 警告 / 0 错误**；`dotnet build UnitTestLumDb -c Debug` → **0 错误**。
- 全量测试（无过滤器，`dotnet test`，13:01 完成）：**391 通过 / 0 失败 / 0 跳过**，耗时 2 m 42 s（控制台原始输出见 `TestEvidence/full_suite_run_20261027_1301.log`）。
  - 既有套件（排除 DefectHunt）：**375 通过 / 0 失败**（修复前 310；其中 `UsageScenarios` 35 个场景 = 65 例，另有 7 个"失败语句"用例按新语义改写）。
  - DefectHunt：**16 条用例全绿**（DH1、DH1b、DH2、DH2b、DH3、DH4×2、DH5×2、DH4b、DH6、DH7、DH8、P1–P3）；修复前为 12 条 6 红。
  - 使用场景（`UnitTestLumDb/UsageScenarios`，35 个场景 / 65 例，Memory + File 双后端）：第一批 18 个场景覆盖 CRUD+重开、`Dispose`=提交、`Discard`=回滚、`Discard` 后复用 / 连续两次 `Discard` 幂等、失败语句作废、async 跨线程提交与释放、并发读 + 写提交、只读隔离、引擎带活事务 `Dispose`、引擎释放后开会话、引擎重复 `Dispose`、`Vacuum`（含事务内调用被拒）、`Add/DropColumn` 重开、事务池 200 次槽位回收、Entity 与 Cell API 互读、空提交/重复提交、同事务两次提交、提交后新读者可见性（DEF-08 回归）；第二批 17 个场景见 §11.5。
- 关键现场（本次复跑写入 `defect_hunt_evidence_20261027.md` 12:3x 段）：
  - DH1：`UnauthorizedAccessException`（提交失败）→ 同/跨线程探针均成功且 < `TimeoutMilliseconds` → 排除故障后新事务提交成功；`k=1/2/3 = True/False/True`，行数 2。
  - DH1b：首次 `SaveChanges` 失败、磁盘恢复后**同一事务**重试成功，行数 2。
  - DH2/DH2b：跨线程 `Dispose` 无异常、租约归还；待提交内容按 `Dispose` 语义落库。
  - DH3：第 33 个事务 <50 ms 抛 `TooManyOpenTransactions`（不含 "disposed"），等待窗口 250 ms 时确实等待 ≥200 ms，归还槽位后可开新事务。
  - DH4/DH5/DH4b：失败语句后 `IsFaulted=True`、后续语句与 `SaveChanges` 均 `TransactionAborted`、库内只有已提交行；`Discard()` 后事务可继续并提交。
  - DH6/DH8：非法策略值 `ArgumentOutOfRangeException`、`Serializable` 双 API `NotSupportedException`；抹签名/9.9.9/50 字节/10 字节垃圾文件分别被 `InvalidDatabaseFile` / `UnsupportedFileVersion`（消息含 "9.9.9"）拒绝。
  - P1–P3 仍绿（本轮只加了读句柄无缓冲这一项，未观察到冷缓存点查与提交吞吐的数量级变化）。
- 版本元数据（13:11）：`LumDbEngine.csproj` 的 `Version` / `AssemblyVersion` / `FileVersion` 由 `3.0.1` 升到 **4.0.0**（页格式仍 1.3.9，`DbHeader.VERSION` 未动；按公开成员 `DbEngine.ReadWriteLock` 移除定为 major）；`README.md` 标题与包版本同步，并补充“事务/错误/上限”“文件与版本”“从 3.0.1 升级”三节，升级节注明 4.0.0 为 major。Release 构建已验证 `FileVersion=4.0.0.0`、产物为 `LumDbEngine.4.0.0.nupkg`。

### 11.5 使用场景第二批（17 个场景 / 29 例，全部绿）

| # | 场景 | 断言的调用方契约 |
|---|---|---|
| 1 | `Scenario_TwoTables_OneTransaction_CommitAndReopen` | 一个事务里建两张表并写入，提交后两张表同时可见、行数与值正确 |
| 2 | `Scenario_BulkInsert_1000Rows_CommitReopenAndUpdate` | 单事务 1000 行一次提交；重开后行数完整、首/中/尾行可按键命中；尾行更新不影响首行 |
| 3 | `Scenario_LargeVarValue_GrowThenShrink_CommitAndReopen` | `StrVar` 从短值增长到 8000+ 字符再缩回：长度、首尾、相邻行在重开后均正确 |
| 4 | `Scenario_ReadOwnUncommittedWrites_ThenDiscardRestoresImage` | 事务能读到自己的插入/更新/删除；`Discard` 后回到已提交镜像且事务仍可继续提交 |
| 5 | `Scenario_DeleteAllRows_ThenReinsert_TableStaysUsable` | 删空 50 行后重插 5 行、再追加 1 行：旧行不复现，表在空态后仍可反复使用 |
| 6 | `Scenario_DeleteThenInsertSameKey_InOneTransaction` | 同事务内删除后立即复用同一 key 成功；复用后唯一约束仍然生效 |
| 7 | `Scenario_ConcurrentWriters_DisjointRows_AllPersisted` | 4 线程 × 10 个单行事务并发提交：无异常、43 行全部落库、抽查键值正确 |
| 8 | `Scenario_LongLivedReader_DelaysCommit_TimeoutThenRetrySucceeds` | 读者持锁时提交按 `TimeoutMilliseconds` 超时失败（不无限等待、不丢脏页）；读者释放后同一事务重试成功 |
| 9 | `Scenario_PoolWait_NewTransactionStartsAfterSlotFreed` | 池满（32）时新事务按 `TransactionPoolWaitMilliseconds` 等待；释放一个槽位后成功启动，之后引擎完全可用 |
| 10 | `Scenario_WriteTransactionOnThreadWithLiveReader_IsRejected` | 同线程已有活事务（含只读）时开写事务被拒；被拒后既有读者仍可用（固化 §11.3 第 4 条） |
| 11 | `Scenario_ReopenLoop_CommittedDataSurvivesFiveRestarts` | 连续 5 次关闭/重开，每轮写 1 行：所有轮次数据在重启后都可读 |
| 12 | `Scenario_SaveToBackup_ReopensCommittedImage` | `SaveTo` 产出可独立打开的完整镜像；再次 `SaveTo` 覆盖旧备份；对源引擎无影响 |
| 13 | `Scenario_GoThroughRowIds_UpdateAndDeleteById` | `GoThrough` 取得行 id 后按 id 更新/删除并提交：命中正确行，其余行不变 |
| 14 | `Scenario_EntityPaging_Count_AndPredicateDelete` | 表达式分页（skip/limit）窗口正确、`Count<T>` 与谓词 `Delete<T>` 计数正确，提交后重开一致 |
| 15 | `Scenario_EntityUpdateAndDeleteById_CommitAndReopen` | `FindByIdEntity` / `UpdateEntity(id)` / `Delete(id)` 全链路，重开后更新与删除均生效 |
| 16 | `Scenario_EmptyCommit_LeavesNoLogFile` | 正常提交后不残留 `.log`；连续空提交不得创建 `.log`（DEF-09 回归） |
| 17 | `Scenario_EmptyCommit_WithLogPathBlocked_DoesNotThrow` | `.log` 路径被目录占位时，空提交/只读事务均不报错（DEF-09 回归）；解除占用后真实提交成功 |

现场数值（写入 `defect_hunt_evidence_20261027.md` 12:58–12:59 段）：1000 行单事务提交并重开校验通过；8004 字符字段增长后长度/首尾一致、缩回 `tiny` 后相邻行不变；4×10 并发写者 43 行全落库；读者持锁提交得到 `Transaction time out.`、释放后同一事务重试成功；池满等待 150 ms 后释放槽位，探针耗时 157 ms（Memory）/ 165 ms（File）并成功。

### 11.6 剩余待决（不阻塞本轮）

1. **1.3.9 之前的文件**是否需要升级器/兼容开关（§8.3、§10.2）；当前实现一律 `UnsupportedFileVersion` 拒绝。
2. `Dispose` 隐式提交与"未 SaveChanges 即丢弃"的使用者直觉冲突：本轮以 README 为准保留提交语义，并用 `Scenario_DisposeWithoutSave_CommitsImplicitly` / `DH2b` 固化；若要改为丢弃，需要同步改 README 与这 3 条用例。
3. 跨线程 `Dispose` 目前静默成功（不抛"契约违规"异常）；DH2 已锁定"不抛 + 租约归还"。
4. `LockTransaction.WriteAction` 对 `State.Read` 仍静默 return（只读事务不调用写路径），保留旧行为。
5. **P2 的 O7 共享页缓存 / O8 组提交 / O9 读写解耦，以及 P3 的 CrashProbe 入 sln、trait 分类、长稳测试：本轮决定不做（用户 2026-10-27 13:07）**。理由：三项都动并发核心（页生命周期/快照回收/提交路径），风险与收益不匹配。已接受的现状：读者存活时提交按 `TimeoutMilliseconds` 超时（PERF-03，13×），可用"缩短读者持锁窗口"规避；小事务提交吞吐维持 153–187 tx/s。