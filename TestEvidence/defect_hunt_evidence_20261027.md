# DefectHunt 现场证据

- 机器: LUOJIN
- 系统: Microsoft Windows NT 10.0.22000.0
- 运行时: 10.0.12
- 采集开始: 2026-10-27 11:03:37.771

## 11:03:37.771 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 0
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 11:03:38.366 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 33
- 重开后连续行数: 35
- 允许区间: [34,35]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
- stderr: 

## 11:03:39.373 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 66
- 重开后连续行数: 68
- 允许区间: [67,68]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 43
C 44
C 45
C 46
C 47
C 48
C 49
C 50
C 51
C 52
C 53
C 54
C 55
C 56
C 57
C 58
C 59
C 60
C 61
C 62
C 63
C 64
C 65
C 66
- stderr: 

## 11:03:40.933 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 122
- 重开后连续行数: 123
- 允许区间: [123,124]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 103
C 104
C 105
C 106
C 107
C 108
C 109
C 110
C 111
C 112
C 113
C 114
C 115
C 116
C 117
C 118
C 119
C 120
C 121
C 122
- stderr: 

## 11:03:43.202 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 211
- 重开后连续行数: 213
- 允许区间: [212,213]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 192
C 193
C 194
C 195
C 196
C 197
C 198
C 199
C 200
C 201
C 202
C 203
C 204
C 205
C 206
C 207
C 208
C 209
C 210
C 211
- stderr: 

## 11:03:43.265 | DH1 提交 IO 失败 -> 写锁泄漏 -> 引擎砖化
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_df4f4785de564238af3b69c4e6dd04e3
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_df4f4785de564238af3b69c4e6dd04e3.log' is denied.
- Dispose 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_df4f4785de564238af3b69c4e6dd04e3.log' is denied.
- 探针异常: LumDbEngine.Element.Exceptions.LumException: In a single-threaded environment, you attempted to use an read only transaction immediately after a normal transaction.
- 探针耗时(ms): 0

## 11:03:49.308 | DH2 跨线程 Dispose 事务 -> 引擎砖化
- Dispose 异常: System.Threading.SynchronizationLockException: The upgradeable lock is being released without being held.
- 探针异常: LumDbEngine.Element.Exceptions.LumException: Transaction time out.
- 探针耗时(ms): 3007

## 11:03:52.334 | DH3 事务池隐性上限 32 / 报错误导
- 第 33 个事务异常: LumDbEngine.Element.Exceptions.LumException: Transaction cannot be accessed beacuse the dbEngine has already be disposed early.
- 失败耗时(ms): 3007
- 引擎实际健康: True
- 报错文本是否声称已释放: True

## 11:03:53.968 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 122.3 us/op
- 每查一个新事务: 702.0 us/op
- 冷/热倍率: 5.74x
- 建表+20k 行写入: 1295 ms

## 11:03:56.340 | P2 小事务提交吞吐
- 文件引擎 单行事务: 153 tx/s
- 文件引擎 空提交（无脏页）: 223 tx/s
- 内存引擎 单行事务: 1429 tx/s
- 样本数: 200

## 11:03:57.542 | P3 只读事务阻塞写提交
- 无读者提交耗时: 9.7 ms
- 读者扫描期间提交耗时: 10.6 ms
- 读者扫描耗时: 11 ms
- 读者扫描行数: 20003
- 提交异常: -

## 11:03:57.551 | DH4 插入失败静默回滚全事务（Memory）
- 重复键异常: LumException: Key already existed: k
- 失败后同事务第二条插入是否成功: False
- 失败后同事务内首行是否可见: False
- 提交后 A 行是否存在: False
- 提交后 B 行是否存在: False
- 提交后表内行数: 0

## 11:03:57.565 | DH4 插入失败静默回滚全事务（File）
- 重复键异常: LumException: Key already existed: k
- 失败后同事务第二条插入是否成功: False
- 失败后同事务内首行是否可见: False
- 提交后 A 行是否存在: False
- 提交后 B 行是否存在: False
- 提交后表内行数: 0

## 11:03:57.588 | DH6 TransactionPolicy.Serializable 是死配置
- 非法枚举值被接受: True
- 两种策略观测序列: ReadCommitted: before=1, after=2 | Serializable: before=1, after=2

## 11:03:57.629 | DH8 文件魔数/版本号从不校验
- 抹掉签名的文件: READ OK: payload
- 篡改版本号的文件: READ OK: payload
- 10 字节垃圾文件: EndOfStreamException: Unable to read beyond the end of the stream.
- 签名文件状态字节: 2
- 版本文件状态字节: 2

## 11:04:31.785 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: 1
- 重开后连续行数: 2
- 允许区间: [2,3]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
- stderr: 

## 11:04:32.368 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 33
- 重开后连续行数: 34
- 允许区间: [34,35]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
- stderr: 

## 11:04:33.352 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 65
- 重开后连续行数: 66
- 允许区间: [66,67]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 42
C 43
C 44
C 45
C 46
C 47
C 48
C 49
C 50
C 51
C 52
C 53
C 54
C 55
C 56
C 57
C 58
C 59
C 60
C 61
C 62
C 63
C 64
C 65
- stderr: 

## 11:04:34.925 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 109
- 重开后连续行数: 110
- 允许区间: [110,111]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 88
C 89
C 90
C 91
C 92
C 93
C 94
C 95
C 96
C 97
C 98
C 99
C 100
C 101
C 102
C 103
C 104
C 105
C 106
C 107
C 108
C 109
- stderr: 

## 11:04:37.198 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 183
- 重开后连续行数: 185
- 允许区间: [184,185]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 164
C 165
C 166
C 167
C 168
C 169
C 170
C 171
C 172
C 173
C 174
C 175
C 176
C 177
C 178
C 179
C 180
C 181
C 182
C 183
- stderr: 

## 11:04:40.270 | DH1 提交 IO 失败 -> 写锁泄漏 -> 引擎砖化
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_e42bc75dec58487da17bd2ebbf9e8b86
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_e42bc75dec58487da17bd2ebbf9e8b86.log' is denied.
- Dispose 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_e42bc75dec58487da17bd2ebbf9e8b86.log' is denied.
- 同线程探针异常: LumDbEngine.Element.Exceptions.LumException: In a single-threaded environment, you attempted to use an read only transaction immediately after a normal transaction.
- 同线程探针耗时(ms): 0
- 跨线程探针异常: LumDbEngine.Element.Exceptions.LumException: Transaction time out.
- 跨线程探针耗时(ms): 2999

## 11:04:46.314 | DH2 跨线程 Dispose 事务 -> 引擎砖化
- Dispose 异常: System.Threading.SynchronizationLockException: The upgradeable lock is being released without being held.
- 探针异常: LumDbEngine.Element.Exceptions.LumException: Transaction time out.
- 探针耗时(ms): 3014

## 11:04:49.334 | DH3 事务池隐性上限 32 / 报错误导
- 第 33 个事务异常: LumDbEngine.Element.Exceptions.LumException: Transaction cannot be accessed beacuse the dbEngine has already be disposed early.
- 失败耗时(ms): 2998
- 引擎实际健康: True
- 报错文本是否声称已释放: True

## 11:04:50.690 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 122.3 us/op
- 每查一个新事务: 437.1 us/op
- 冷/热倍率: 3.57x
- 建表+20k 行写入: 1121 ms

## 11:04:52.470 | P2 小事务提交吞吐
- 文件引擎 单行事务: 187 tx/s
- 文件引擎 空提交（无脏页）: 336 tx/s
- 内存引擎 单行事务: 2238 tx/s
- 样本数: 200

## 11:04:53.614 | P3 只读事务阻塞写提交
- 无读者提交耗时: 8.5 ms
- 读者扫描期间提交耗时: 9.6 ms
- 读者扫描耗时: 19 ms
- 读者扫描行数: 20003
- 提交异常: -

## 11:04:53.624 | DH4 插入失败静默回滚全事务（Memory）
- 重复键异常: LumException: Key already existed: k
- 失败后同事务第二条插入是否成功: False
- 失败后同事务内首行是否可见: False
- 提交后 A 行是否存在: False
- 提交后 B 行是否存在: False
- 提交后表内行数: 0

## 11:04:53.638 | DH4 插入失败静默回滚全事务（File）
- 重复键异常: LumException: Key already existed: k
- 失败后同事务第二条插入是否成功: False
- 失败后同事务内首行是否可见: False
- 提交后 A 行是否存在: False
- 提交后 B 行是否存在: False
- 提交后表内行数: 0

## 11:04:53.647 | DH5 Entity API 同一失败不丢数据（与 DH4 语义不一致）
- 重复键异常: LumException: Key already existed: Code
- 提交后 A 行是否存在: True
- 提交后 B 行是否存在: True

## 11:04:53.651 | DH6 TransactionPolicy.Serializable 是死配置
- 非法枚举值被接受: True
- 两种策略观测序列: ReadCommitted: before=1, after=2 | Serializable: before=1, after=2

## 11:04:53.691 | DH8 文件魔数/版本号从不校验
- 抹掉签名的文件: READ OK: payload
- 篡改版本号的文件: READ OK: payload
- 10 字节垃圾文件: EndOfStreamException: Unable to read beyond the end of the stream.
- 签名文件状态字节: 2
- 版本文件状态字节: 2

## 11:05:25.285 | P3 只读事务阻塞写提交
- 无读者提交耗时: 13.6 ms
- 读者扫描期间提交耗时: 181.3 ms
- 读者扫描耗时: 195 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:27:37.031 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: ca6eb1c5-d924-4fa0-a1c7-beca1c3ffa87
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:27:37.169 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: a179bd48-897a-442b-9b1f-fe40a9bf501e
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:35:48.490 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 0
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 12:35:49.077 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 18
- 重开后连续行数: 20
- 允许区间: [19,20]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
- stderr: 

## 12:35:50.072 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 34
- 重开后连续行数: 35
- 允许区间: [35,36]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
C 34
- stderr: 

## 12:35:51.644 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 76
- 重开后连续行数: 78
- 允许区间: [77,78]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 53
C 54
C 55
C 56
C 57
C 58
C 59
C 60
C 61
C 62
C 63
C 64
C 65
C 66
C 67
C 68
C 69
C 70
C 71
C 72
C 73
C 74
C 75
C 76
- stderr: 

## 12:35:53.924 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 115
- 重开后连续行数: 116
- 允许区间: [116,117]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 95
C 96
C 97
C 98
C 99
C 100
C 101
C 102
C 103
C 104
C 105
C 106
C 107
C 108
C 109
C 110
C 111
C 112
C 113
C 114
C 115
- stderr: 

## 12:35:54.047 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_a600e8fec18149958a1a0ce046e9657e
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_a600e8fec18149958a1a0ce046e9657e.log' is denied.
- SaveChanges 耗时(ms): 1
- Dispose 异常: <null>
- 同线程探针异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_a600e8fec18149958a1a0ce046e9657e.log' is denied.
- 同线程探针耗时(ms): 7
- 跨线程探针异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_a600e8fec18149958a1a0ce046e9657e.log' is denied.
- 跨线程探针耗时(ms): 7
- 排除故障后重试异常: <null>
- 重试耗时(ms): 16
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:35:54.077 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:35:54.081 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:35:54.344 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 249
- 归还槽位后异常: LumDbEngine.Element.Exceptions.LumException: In a single-threaded environment, you attempted to use an read only transaction immediately after a normal transaction.
- 归还槽位后耗时(ms): 0

## 12:36:09.441 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 5097.4 us/op
- 每查一个新事务: 29496.0 us/op
- 冷/热倍率: 5.79x
- 建表+20k 行写入: 1228 ms

## 12:36:13.121 | P2 小事务提交吞吐
- 文件引擎 单行事务: 68 tx/s
- 文件引擎 空提交（无脏页）: 325 tx/s
- 内存引擎 单行事务: 2363 tx/s
- 样本数: 200

## 12:36:14.838 | P3 只读事务阻塞写提交
- 无读者提交耗时: 64.2 ms
- 读者扫描期间提交耗时: 420.9 ms
- 读者扫描耗时: 435 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:36:14.851 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:36:14.895 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:36:14.899 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:36:14.938 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:36:14.950 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:36:14.991 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.

---

## 修复后复跑结论（2026-10-27 12:35–12:50，人工汇总，非用例输出）

- 本文件 11:03–11:05 的条目是**修复前**现场（只加测试阶段），12:35 起的条目是**源码修改后**的现场，两段可直接对照。
- 12:3x 全量：**362 通过 / 0 失败 / 0 跳过**（既有 + 使用场景 346，DefectHunt 16）；扩展使用场景后的 13:01 最终全量为 **391 通过 / 0 失败**（见文末“最终验收”）。
- 逐条：DH1、DH1b、DH2、DH2b、DH3、DH4(M/F)、DH5(M/F)、DH4b、DH6、DH7、DH8、P1、P2、P3 **全绿**。

| 观测点 | 修复前 | 修复后 |
|---|---|---|
| 提交 IO 失败后的同线程新事务 | 0 ms 抛 `IllegaTransaction`（写锁泄漏） | 成功，耗时 < 1 ms |
| 提交 IO 失败后的跨线程新事务 | 等满 3 s 抛 `Transaction time out` | 成功 |
| 同一事务在磁盘恢复后重试 `SaveChanges` | 未覆盖（写锁已泄漏，无法重试） | 成功（DH1b） |
| 跨线程 `Dispose` | `SynchronizationLockException`，引擎报废 | 无异常，租约归还，引擎可用（DH2/DH2b） |
| 第 33 个并发事务 | 等 3 s 抛"引擎已释放" | < 50 ms 抛 `TooManyOpenTransactions`（含上限 32）（DH3） |
| 失败语句后的同事务语句 / `SaveChanges` | 静默 `Reset` 后"成功提交 0 行"（Entity 路径反而全部提交） | 抛 `TransactionAborted`，库内只有已提交行（DH4/DH5/DH4b） |
| 抹签名 / 版本 9.9.9 / 截断 50 字节 / 10 字节垃圾文件 | 全部 `READ OK` | `InvalidDatabaseFile` / `UnsupportedFileVersion`（含 "9.9.9"）（DH8） |
| `STATE=Writing` 且无 `.log` 重开 | 页可读，但内存 `header.State` 仍是 `Writing(3)` | 状态字节修回 `Done(2)`，内存与磁盘一致（DEF-08 修复后） |

## 12:37:16.934 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 0
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 12:37:17.515 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 25
- 重开后连续行数: 26
- 允许区间: [26,27]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
- stderr: 

## 12:37:18.482 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 44
- 重开后连续行数: 45
- 允许区间: [45,46]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
C 34
C 35
C 36
C 37
C 38
C 39
C 40
C 41
C 42
C 43
C 44
- stderr: 

## 12:37:20.051 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 92
- 重开后连续行数: 93
- 允许区间: [93,94]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 69
C 70
C 71
C 72
C 73
C 74
C 75
C 76
C 77
C 78
C 79
C 80
C 81
C 82
C 83
C 84
C 85
C 86
C 87
C 88
C 89
C 90
C 91
C 92
- stderr: 

## 12:37:22.329 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 139
- 重开后连续行数: 140
- 允许区间: [140,141]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 120
C 121
C 122
C 123
C 124
C 125
C 126
C 127
C 128
C 129
C 130
C 131
C 132
C 133
C 134
C 135
C 136
C 137
C 138
C 139
- stderr: 

## 12:37:22.430 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_dc76f782831f4e22a6e345a45899f3b8
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_dc76f782831f4e22a6e345a45899f3b8.log' is denied.
- SaveChanges 耗时(ms): 1
- Dispose 异常: <null>
- 同线程探针异常: <null>
- 同线程探针耗时(ms): 5
- 跨线程探针异常: <null>
- 跨线程探针耗时(ms): 5
- 排除故障后重试异常: <null>
- 重试耗时(ms): 12
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:37:22.439 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:37:22.443 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:37:22.696 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 239
- 归还槽位后异常: <null>
- 归还槽位后耗时(ms): 1

## 12:37:37.345 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 5290.4 us/op
- 每查一个新事务: 28637.3 us/op
- 冷/热倍率: 5.41x
- 建表+20k 行写入: 1050 ms

## 12:37:42.012 | P2 小事务提交吞吐
- 文件引擎 单行事务: 44 tx/s
- 文件引擎 空提交（无脏页）: 62058 tx/s
- 内存引擎 单行事务: 2000 tx/s
- 样本数: 200

## 12:37:43.587 | P3 只读事务阻塞写提交
- 无读者提交耗时: 48.2 ms
- 读者扫描期间提交耗时: 338.7 ms
- 读者扫描耗时: 362 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:37:43.602 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:37:43.649 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:37:43.655 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:37:43.693 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:37:43.697 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:37:43.728 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.

## 12:37:53.541 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: b4637574-273c-4169-a42c-c0bbd839ef90
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:37:53.667 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 21ba7ab3-aebc-4556-b493-72b94808f910
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:40:03.526 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: e550056a-68bd-4d77-bbd5-439410fe985c
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:40:03.653 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 5a39b5fb-e070-4e71-8f58-5fd7c843edd8
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:42:15.976 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: d6818973-b72e-4987-a785-10db2dbb976c
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:42:16.122 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 44da85d6-0211-4282-9fe3-ce6017d47466
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:42:21.310 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 1
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 12:42:21.901 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 21
- 重开后连续行数: 23
- 允许区间: [22,23]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
- stderr: 

## 12:42:22.877 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 39
- 重开后连续行数: 40
- 允许区间: [40,41]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
C 34
C 35
C 36
C 37
C 38
C 39
- stderr: 

## 12:42:24.442 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 86
- 重开后连续行数: 87
- 允许区间: [87,88]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 63
C 64
C 65
C 66
C 67
C 68
C 69
C 70
C 71
C 72
C 73
C 74
C 75
C 76
C 77
C 78
C 79
C 80
C 81
C 82
C 83
C 84
C 85
C 86
- stderr: 

## 12:42:26.734 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 133
- 重开后连续行数: 135
- 允许区间: [134,135]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 114
C 115
C 116
C 117
C 118
C 119
C 120
C 121
C 122
C 123
C 124
C 125
C 126
C 127
C 128
C 129
C 130
C 131
C 132
C 133
- stderr: 

## 12:42:26.803 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_98c4743b241d4d2587b03fbbc0e6fda0
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_98c4743b241d4d2587b03fbbc0e6fda0.log' is denied.
- SaveChanges 耗时(ms): 0
- Dispose 异常: <null>
- 同线程探针异常: <null>
- 同线程探针耗时(ms): 6
- 跨线程探针异常: <null>
- 跨线程探针耗时(ms): 5
- 排除故障后重试异常: <null>
- 重试耗时(ms): 11
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:42:26.807 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:42:26.811 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:42:27.074 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 253
- 归还槽位后异常: <null>
- 归还槽位后耗时(ms): 0

## 12:42:44.569 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 5667.2 us/op
- 每查一个新事务: 35331.3 us/op
- 冷/热倍率: 6.23x
- 建表+20k 行写入: 1088 ms

## 12:42:47.443 | P2 小事务提交吞吐
- 文件引擎 单行事务: 73 tx/s
- 文件引擎 空提交（无脏页）: 63886 tx/s
- 内存引擎 单行事务: 2182 tx/s
- 样本数: 200

## 12:42:49.830 | P3 只读事务阻塞写提交
- 无读者提交耗时: 208.9 ms
- 读者扫描期间提交耗时: 577.9 ms
- 读者扫描耗时: 580 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:42:49.840 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:42:49.894 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:42:49.899 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:42:49.950 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:42:49.955 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:42:50.008 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.

## 12:43:19.355 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 0
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 12:43:19.963 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 15
- 重开后连续行数: 16
- 允许区间: [16,17]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
- stderr: 

## 12:43:20.967 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 34
- 重开后连续行数: 35
- 允许区间: [35,36]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
C 34
- stderr: 

## 12:43:22.547 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 68
- 重开后连续行数: 69
- 允许区间: [69,70]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 45
C 46
C 47
C 48
C 49
C 50
C 51
C 52
C 53
C 54
C 55
C 56
C 57
C 58
C 59
C 60
C 61
C 62
C 63
C 64
C 65
C 66
C 67
C 68
- stderr: 

## 12:43:24.843 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 108
- 重开后连续行数: 109
- 允许区间: [109,110]
- 载荷校验: True
- 错误: -
- stdout 尾部: 6
C 87
C 88
C 89
C 90
C 91
C 92
C 93
C 94
C 95
C 96
C 97
C 98
C 99
C 100
C 101
C 102
C 103
C 104
C 105
C 106
C 107
C 108
- stderr: 

## 12:43:24.974 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_e22f024df6c441f394c6baf53cf92efc
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_e22f024df6c441f394c6baf53cf92efc.log' is denied.
- SaveChanges 耗时(ms): 1
- Dispose 异常: <null>
- 同线程探针异常: <null>
- 同线程探针耗时(ms): 8
- 跨线程探针异常: <null>
- 跨线程探针耗时(ms): 9
- 排除故障后重试异常: <null>
- 重试耗时(ms): 15
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:43:24.985 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:43:24.989 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:43:25.275 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 263
- 归还槽位后异常: <null>
- 归还槽位后耗时(ms): 1

## 12:43:47.188 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 8111.4 us/op
- 每查一个新事务: 43447.8 us/op
- 冷/热倍率: 5.36x
- 建表+20k 行写入: 1278 ms

## 12:43:56.822 | P2 小事务提交吞吐
- 文件引擎 单行事务: 24 tx/s
- 文件引擎 空提交（无脏页）: 1188 tx/s
- 内存引擎 单行事务: 181 tx/s
- 样本数: 200

## 12:43:59.848 | P3 只读事务阻塞写提交
- 无读者提交耗时: 168.3 ms
- 读者扫描期间提交耗时: 919.2 ms
- 读者扫描耗时: 944 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:43:59.873 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:43:59.968 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:43:59.982 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:44:00.079 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:44:00.091 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:44:00.161 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.

## 12:45:45.467 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 0
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 12:45:46.063 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 13
- 重开后连续行数: 14
- 允许区间: [14,15]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
- stderr: 

## 12:45:47.041 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 26
- 重开后连续行数: 27
- 允许区间: [27,28]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
- stderr: 

## 12:45:48.598 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 56
- 重开后连续行数: 58
- 允许区间: [57,58]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 33
C 34
C 35
C 36
C 37
C 38
C 39
C 40
C 41
C 42
C 43
C 44
C 45
C 46
C 47
C 48
C 49
C 50
C 51
C 52
C 53
C 54
C 55
C 56
- stderr: 

## 12:45:50.898 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 106
- 重开后连续行数: 107
- 允许区间: [107,108]
- 载荷校验: True
- 错误: -
- stdout 尾部:  84
C 85
C 86
C 87
C 88
C 89
C 90
C 91
C 92
C 93
C 94
C 95
C 96
C 97
C 98
C 99
C 100
C 101
C 102
C 103
C 104
C 105
C 106
- stderr: 

## 12:45:51.040 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_2f38626bc2b04917a1ed381c0722aace
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_2f38626bc2b04917a1ed381c0722aace.log' is denied.
- SaveChanges 耗时(ms): 1
- Dispose 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_2f38626bc2b04917a1ed381c0722aace.log' is denied.
- 同线程探针异常: <null>
- 同线程探针耗时(ms): 8
- 跨线程探针异常: <null>
- 跨线程探针耗时(ms): 8
- 排除故障后重试异常: <null>
- 重试耗时(ms): 15
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:45:51.083 | DH1b 提交 IO 失败 -> 脏页保留 -> 同一事务重试成功
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_3c3ac223a2d24298a3229e87c8796531
- 首次 SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_3c3ac223a2d24298a3229e87c8796531.log' is denied.
- 磁盘恢复后同一事务重试异常: <null>
- Dispose 异常: <null>
- k=1 / k=2 是否存在: True/True
- 行数: 2

## 12:45:51.094 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:45:51.098 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:45:51.381 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 263
- 归还槽位后异常: <null>
- 归还槽位后耗时(ms): 1

## 12:46:09.104 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 6299.2 us/op
- 每查一个新事务: 35052.1 us/op
- 冷/热倍率: 5.56x
- 建表+20k 行写入: 1154 ms

## 12:46:12.430 | P2 小事务提交吞吐
- 文件引擎 单行事务: 63 tx/s
- 文件引擎 空提交（无脏页）: 47762 tx/s
- 内存引擎 单行事务: 1361 tx/s
- 样本数: 200

## 12:46:14.171 | P3 只读事务阻塞写提交
- 无读者提交耗时: 75.1 ms
- 读者扫描期间提交耗时: 364.1 ms
- 读者扫描耗时: 380 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:46:14.183 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:46:14.223 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:46:14.228 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:46:14.263 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:46:14.267 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:46:14.297 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.

## 12:46:24.251 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 23565456-a821-4d1a-843c-0c567f8c24c9
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:46:24.406 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 4a1a68ce-4c84-4199-990b-2f773c4230d9
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:46:29.878 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 0
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 12:46:30.442 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 27
- 重开后连续行数: 28
- 允许区间: [28,29]
- 载荷校验: True
- 错误: -
- stdout 尾部:  2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
- stderr: 

## 12:46:31.485 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 39
- 重开后连续行数: 40
- 允许区间: [40,41]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
C 34
C 35
C 36
C 37
C 38
C 39
- stderr: 

## 12:46:33.103 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 56
- 重开后连续行数: 58
- 允许区间: [57,58]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 33
C 34
C 35
C 36
C 37
C 38
C 39
C 40
C 41
C 42
C 43
C 44
C 45
C 46
C 47
C 48
C 49
C 50
C 51
C 52
C 53
C 54
C 55
C 56
- stderr: 

## 12:46:35.383 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 146
- 重开后连续行数: 148
- 允许区间: [147,148]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 127
C 128
C 129
C 130
C 131
C 132
C 133
C 134
C 135
C 136
C 137
C 138
C 139
C 140
C 141
C 142
C 143
C 144
C 145
C 146
- stderr: 

## 12:46:35.435 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_bfc1eeb395834e24ae713489bdb09599
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_bfc1eeb395834e24ae713489bdb09599.log' is denied.
- SaveChanges 耗时(ms): 0
- Dispose 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_bfc1eeb395834e24ae713489bdb09599.log' is denied.
- 同线程探针异常: <null>
- 同线程探针耗时(ms): 5
- 跨线程探针异常: <null>
- 跨线程探针耗时(ms): 5
- 排除故障后重试异常: <null>
- 重试耗时(ms): 11
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:46:35.465 | DH1b 提交 IO 失败 -> 脏页保留 -> 同一事务重试成功
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_db24e99395e2421ba08b09f2b54e230d
- 首次 SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_db24e99395e2421ba08b09f2b54e230d.log' is denied.
- 磁盘恢复后同一事务重试异常: <null>
- Dispose 异常: <null>
- k=1 / k=2 是否存在: True/True
- 行数: 2

## 12:46:35.467 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:46:35.470 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:46:35.737 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 258
- 归还槽位后异常: <null>
- 归还槽位后耗时(ms): 0

## 12:46:50.792 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 5780.7 us/op
- 每查一个新事务: 29139.4 us/op
- 冷/热倍率: 5.04x
- 建表+20k 行写入: 1081 ms

## 12:46:54.492 | P2 小事务提交吞吐
- 文件引擎 单行事务: 56 tx/s
- 文件引擎 空提交（无脏页）: 72438 tx/s
- 内存引擎 单行事务: 2057 tx/s
- 样本数: 200

## 12:46:55.997 | P3 只读事务阻塞写提交
- 无读者提交耗时: 46.3 ms
- 读者扫描期间提交耗时: 293.0 ms
- 读者扫描耗时: 314 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:46:56.004 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:46:56.037 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:46:56.040 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:46:56.069 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:46:56.072 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:46:56.100 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.

## 12:49:38.979 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: ae4e3980-0adc-4faa-b507-448b52e5565a
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:49:39.106 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 3b2e0b08-54dc-49ea-839e-137d86f3d95b
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:49:54.130 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: fa925d11-aac1-4680-a6d5-79076bb26441
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:49:54.261 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: b012279c-d56c-4cea-ba96-32c27cbf2008
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:49:58.003 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: 1
- 重开后连续行数: 2
- 允许区间: [2,3]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
- stderr: 

## 12:49:58.569 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 24
- 重开后连续行数: 25
- 允许区间: [25,26]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
- stderr: 

## 12:49:59.536 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 49
- 重开后连续行数: 50
- 允许区间: [50,51]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
C 34
C 35
C 36
C 37
C 38
C 39
C 40
C 41
C 42
C 43
C 44
C 45
C 46
C 47
C 48
C 49
- stderr: 

## 12:50:01.159 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 75
- 重开后连续行数: 76
- 允许区间: [76,77]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 52
C 53
C 54
C 55
C 56
C 57
C 58
C 59
C 60
C 61
C 62
C 63
C 64
C 65
C 66
C 67
C 68
C 69
C 70
C 71
C 72
C 73
C 74
C 75
- stderr: 

## 12:50:03.451 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 113
- 重开后连续行数: 114
- 允许区间: [114,115]
- 载荷校验: True
- 错误: -
- stdout 尾部: 2
C 93
C 94
C 95
C 96
C 97
C 98
C 99
C 100
C 101
C 102
C 103
C 104
C 105
C 106
C 107
C 108
C 109
C 110
C 111
C 112
C 113
- stderr: 

## 12:50:03.508 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_b270ac98e4c64e48ab4e51df345ca953
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_b270ac98e4c64e48ab4e51df345ca953.log' is denied.
- SaveChanges 耗时(ms): 0
- Dispose 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_b270ac98e4c64e48ab4e51df345ca953.log' is denied.
- 同线程探针异常: <null>
- 同线程探针耗时(ms): 6
- 跨线程探针异常: <null>
- 跨线程探针耗时(ms): 9
- 排除故障后重试异常: <null>
- 重试耗时(ms): 10
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:50:03.541 | DH1b 提交 IO 失败 -> 脏页保留 -> 同一事务重试成功
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_b3a4304d1f0d47c088b886bb69534233
- 首次 SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_b3a4304d1f0d47c088b886bb69534233.log' is denied.
- 磁盘恢复后同一事务重试异常: <null>
- Dispose 异常: <null>
- k=1 / k=2 是否存在: True/True
- 行数: 2

## 12:50:03.546 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:50:03.549 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:50:03.820 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 256
- 归还槽位后异常: <null>
- 归还槽位后耗时(ms): 0

## 12:50:20.770 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 8009.9 us/op
- 每查一个新事务: 31622.2 us/op
- 冷/热倍率: 3.95x
- 建表+20k 行写入: 1091 ms

## 12:50:23.807 | P2 小事务提交吞吐
- 文件引擎 单行事务: 68 tx/s
- 文件引擎 空提交（无脏页）: 64807 tx/s
- 内存引擎 单行事务: 2388 tx/s
- 样本数: 200

## 12:50:25.358 | P3 只读事务阻塞写提交
- 无读者提交耗时: 49.4 ms
- 读者扫描期间提交耗时: 383.6 ms
- 读者扫描耗时: 367 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:50:25.364 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:50:25.447 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:50:25.450 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:50:25.477 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:50:25.480 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:50:25.505 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.

## 12:56:28.386 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 74220549-6175-4f44-90d6-c93be13003f2
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:56:28.530 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: e5d465c3-4cfe-42de-aba8-9da8c1a3aee3
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:56:50.378 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 4727e9fc-8d55-44bb-ac9c-2d4ad4313747
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:56:50.505 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 97655305-5b67-449c-b70b-5587a86631d8
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:57:58.482 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 74f22e34-e8a8-4920-9388-00fb9eb6d4e7
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:57:58.622 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 240b453d-10af-43c5-8969-0d823330a2fa
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:58:24.571 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: cc586dc5-560b-44fc-a2cd-44ec490be587
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:58:24.726 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 3cb356d1-ecd4-428e-8118-24de5db568db
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:58:58.445 | 使用场景：单事务批量插入（Memory）
- 行数: 1000
- 提交方式: 单事务一次 SaveChanges
- 重启后校验: 通过

## 12:58:58.637 | 使用场景：单事务批量插入（File）
- 行数: 1000
- 提交方式: 单事务一次 SaveChanges
- 重启后校验: 通过

## 12:58:58.646 | 使用场景：跨页大字段增长/收缩（Memory）
- 大字段长度: 8004
- 增长后重开: 长度与首尾一致
- 收缩后重开: tiny

## 12:58:58.756 | 使用场景：跨页大字段增长/收缩（File）
- 大字段长度: 8004
- 增长后重开: 长度与首尾一致
- 收缩后重开: tiny

## 12:58:59.084 | 使用场景：并发写入者（Memory）
- 写者: 4
- 每个写者事务数: 10
- 异常: 0
- 最终行数: 43

## 12:58:59.776 | 使用场景：并发写入者（File）
- 写者: 4
- 每个写者事务数: 10
- 异常: 0
- 最终行数: 43

## 12:58:59.998 | 使用场景：长活读者阻塞提交（Memory）
- 首次提交异常: LumDbEngine.Element.Exceptions.LumException: Transaction time out.
- 锁等待超时(ms): 200
- 读者释放后重试: 成功

## 12:59:00.240 | 使用场景：长活读者阻塞提交（File）
- 首次提交异常: LumDbEngine.Element.Exceptions.LumException: Transaction time out.
- 锁等待超时(ms): 200
- 读者释放后重试: 成功

## 12:59:00.417 | 使用场景：事务池等待（Memory）
- 并发上限: 32
- 池满后等待(ms): 150
- 槽位释放后探针耗时(ms): 157
- 探针结果: 成功

## 12:59:00.602 | 使用场景：事务池等待（File）
- 并发上限: 32
- 池满后等待(ms): 150
- 槽位释放后探针耗时(ms): 165
- 探针结果: 成功

## 12:59:00.787 | 使用场景：五次关闭/重开循环
- 重启次数: 5
- 最终行数: 5
- 每轮写入: 1 行 + SaveChanges

## 12:59:01.092 | 使用场景：日志路径被目录占位时的空提交
- 空提交次数: 3
- 异常: 无
- 日志文件存在: False

## 12:59:02.726 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: f88b6c35-dcac-42ec-85ae-08bbc28ad549
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:59:02.855 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: c3c3844c-be94-40b3-a286-39333d99cb02
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:59:12.040 | 使用场景：单事务批量插入（Memory）
- 行数: 1000
- 提交方式: 单事务一次 SaveChanges
- 重启后校验: 通过

## 12:59:12.200 | 使用场景：单事务批量插入（File）
- 行数: 1000
- 提交方式: 单事务一次 SaveChanges
- 重启后校验: 通过

## 12:59:12.211 | 使用场景：跨页大字段增长/收缩（Memory）
- 大字段长度: 8004
- 增长后重开: 长度与首尾一致
- 收缩后重开: tiny

## 12:59:12.298 | 使用场景：跨页大字段增长/收缩（File）
- 大字段长度: 8004
- 增长后重开: 长度与首尾一致
- 收缩后重开: tiny

## 12:59:12.605 | 使用场景：并发写入者（Memory）
- 写者: 4
- 每个写者事务数: 10
- 异常: 0
- 最终行数: 43

## 12:59:13.343 | 使用场景：并发写入者（File）
- 写者: 4
- 每个写者事务数: 10
- 异常: 0
- 最终行数: 43

## 12:59:13.552 | 使用场景：长活读者阻塞提交（Memory）
- 首次提交异常: LumDbEngine.Element.Exceptions.LumException: Transaction time out.
- 锁等待超时(ms): 200
- 读者释放后重试: 成功

## 12:59:13.792 | 使用场景：长活读者阻塞提交（File）
- 首次提交异常: LumDbEngine.Element.Exceptions.LumException: Transaction time out.
- 锁等待超时(ms): 200
- 读者释放后重试: 成功

## 12:59:13.970 | 使用场景：事务池等待（Memory）
- 并发上限: 32
- 池满后等待(ms): 150
- 槽位释放后探针耗时(ms): 159
- 探针结果: 成功

## 12:59:14.160 | 使用场景：事务池等待（File）
- 并发上限: 32
- 池满后等待(ms): 150
- 槽位释放后探针耗时(ms): 174
- 探针结果: 成功

## 12:59:14.307 | 使用场景：五次关闭/重开循环
- 重启次数: 5
- 最终行数: 5
- 每轮写入: 1 行 + SaveChanges

## 12:59:14.583 | 使用场景：日志路径被目录占位时的空提交
- 空提交次数: 3
- 异常: 无
- 日志文件存在: False

## 12:59:16.219 | 引擎 Dispose 与活事务的交互（Memory）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 2ca7ef23-6a0a-491f-b443-29e6618737f4
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:59:16.362 | 引擎 Dispose 与活事务的交互（File）
- 有活事务时 eng.Dispose 异常: LumException: Waiting living transactions  timeout when disposing DbEngine. Living transactions: 2ab00313-d975-4d05-b910-63910e930da4
- 随后 tx.Dispose 异常: 
- 再次 eng.Dispose 异常: 

## 12:59:21.046 | DH7 崩溃矩阵 200ms
- 结果: OK
- lastCommitted: -1
- 重开后连续行数: 0
- 允许区间: [0,1]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
- stderr: 

## 12:59:21.669 | DH7 崩溃矩阵 500ms
- 结果: OK
- lastCommitted: 19
- 重开后连续行数: 20
- 允许区间: [20,21]
- 载荷校验: True
- 错误: -
- stdout 尾部: C 0
C 1
C 2
C 3
C 4
C 5
C 6
C 7
C 8
C 9
C 10
C 11
C 12
C 13
C 14
C 15
C 16
C 17
C 18
C 19
- stderr: 

## 12:59:22.659 | DH7 崩溃矩阵 900ms
- 结果: OK
- lastCommitted: 38
- 重开后连续行数: 39
- 允许区间: [39,40]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 15
C 16
C 17
C 18
C 19
C 20
C 21
C 22
C 23
C 24
C 25
C 26
C 27
C 28
C 29
C 30
C 31
C 32
C 33
C 34
C 35
C 36
C 37
C 38
- stderr: 

## 12:59:24.235 | DH7 崩溃矩阵 1500ms
- 结果: OK
- lastCommitted: 90
- 重开后连续行数: 91
- 允许区间: [91,92]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 67
C 68
C 69
C 70
C 71
C 72
C 73
C 74
C 75
C 76
C 77
C 78
C 79
C 80
C 81
C 82
C 83
C 84
C 85
C 86
C 87
C 88
C 89
C 90
- stderr: 

## 12:59:26.555 | DH7 崩溃矩阵 2200ms
- 结果: OK
- lastCommitted: 135
- 重开后连续行数: 136
- 允许区间: [136,137]
- 载荷校验: True
- 错误: -
- stdout 尾部: 
C 116
C 117
C 118
C 119
C 120
C 121
C 122
C 123
C 124
C 125
C 126
C 127
C 128
C 129
C 130
C 131
C 132
C 133
C 134
C 135
- stderr: 

## 12:59:26.618 | DH1 提交 IO 失败 -> 抛异常但不砖化、可重试
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_7e3e9c4b439b4f3baf6d3a7cc58a98a2
- SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_7e3e9c4b439b4f3baf6d3a7cc58a98a2.log' is denied.
- SaveChanges 耗时(ms): 0
- Dispose 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1_7e3e9c4b439b4f3baf6d3a7cc58a98a2.log' is denied.
- 同线程探针异常: <null>
- 同线程探针耗时(ms): 7
- 跨线程探针异常: <null>
- 跨线程探针耗时(ms): 6
- 排除故障后重试异常: <null>
- 重试耗时(ms): 12
- 提交后 k=1/2/3 是否存在: True/False/True
- 提交后行数: 2

## 12:59:26.651 | DH1b 提交 IO 失败 -> 脏页保留 -> 同一事务重试成功
- db 路径: C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_df9c966af49c400aa248bb440b8622b4
- 首次 SaveChanges 异常: System.UnauthorizedAccessException: Access to the path 'C:\Users\luoji\AppData\Local\Temp\lumdb_defecthunt\dh1b_df9c966af49c400aa248bb440b8622b4.log' is denied.
- 磁盘恢复后同一事务重试异常: <null>
- Dispose 异常: <null>
- k=1 / k=2 是否存在: True/True
- 行数: 2

## 12:59:26.654 | DH2 跨线程 Dispose -> 租约归还、引擎可用
- Dispose 异常: <null>
- Dispose 耗时(ms): 0
- 探针异常: <null>
- 探针耗时(ms): 0

## 12:59:26.657 | DH2b 跨线程 Dispose 未提交事务 -> 按 Dispose 提交语义落库且不抛异常
- Dispose 异常: <null>
- 未提交行是否按 Dispose 语义落库: True

## 12:59:26.920 | DH3 事务池第 33 个事务：快速失败 + 准确错误 + 可恢复
- 池上限: 32
- 第 33 个异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 第 33 个耗时(ms): 0
- 错误是否谎称 disposed: False
- 引擎实际健康: True
- 配置 250ms 等待后的异常: LumDbEngine.Element.Exceptions.LumException: Too many open transactions. Dispose finished transactions before starting new ones. Limit: 32, currently open: 32.
- 等待耗时(ms): 252
- 归还槽位后异常: <null>
- 归还槽位后耗时(ms): 0

## 12:59:42.422 | P1 每事务冷缓存：点查成本
- 行数: 20000
- 同事务预热后: 5741.9 us/op
- 每查一个新事务: 30269.5 us/op
- 冷/热倍率: 5.27x
- 建表+20k 行写入: 1091 ms

## 12:59:45.437 | P2 小事务提交吞吐
- 文件引擎 单行事务: 70 tx/s
- 文件引擎 空提交（无脏页）: 54888 tx/s
- 内存引擎 单行事务: 1593 tx/s
- 样本数: 200

## 12:59:47.157 | P3 只读事务阻塞写提交
- 无读者提交耗时: 58.7 ms
- 读者扫描期间提交耗时: 431.7 ms
- 读者扫描耗时: 449 ms
- 读者扫描行数: 20003
- 提交异常: -

## 12:59:47.166 | DH4 失败语句作废整个事务（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:59:47.209 | DH4 失败语句作废整个事务（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: k
- 事务是否被标记作废: True
- 作废后继续插入的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: k
- Dispose 异常: 

## 12:59:47.213 | DH5 Entity API 与 DbCell API 语义一致（Memory）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:59:47.251 | DH5 Entity API 与 DbCell API 语义一致（File）
- 重复键异常: LumDbEngine.Element.Exceptions.LumException: Key already existed: Code
- 事务是否被标记作废: True
- 作废后 SaveChanges 的异常: LumDbEngine.Element.Exceptions.LumException: The transaction was aborted by a previous failed statement and cannot be used any more. Dispose it (its changes will not be committed) or call Discard() to reuse it. First failure: Key already existed: Code

## 12:59:47.255 | DH6 TransactionPolicy 非法值被拒绝 / Serializable 显式不支持
- 赋值 (TransactionPolicy)12345 的异常: System.ArgumentOutOfRangeException: Unknown transaction policy. (Parameter 'value')
Actual value was 12345.
- Serializable 下 StartTransaction 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- Serializable 下 StartTransactionReadonly 的异常: System.NotSupportedException: Serializable transactions are not implemented. Use TransactionPolicy.ReadCommitted.
- 切回 ReadCommitted 后的异常: <null>

## 12:59:47.290 | DH8 文件签名 / 页面格式版本 / 长度受校验
- 正常文件（对照）: READ OK: payload
- 抹掉签名的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Signature check failed.
- 版本号改写为 9.9.9 的文件: LumException: Unsupported database file version. File format 9.9.9, this build writes and reads 1.3.9.
- 截断到 50 字节的文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 50 bytes, expected at least 100.
- 10 字节垃圾文件: LumException: The file is not a valid LumDb database (header signature or size is invalid). Length is 10 bytes, expected at least 100.


---

## 最终验收（2026-10-27 13:01，全量复跑，人工汇总，非用例输出）

- 命令：`dotnet test UnitTestLumDb\UnitTestLumDb.csproj -c Debug --no-build`
- 结果：**391 通过 / 0 失败 / 0 跳过**，耗时 2 m 42 s；原始控制台输出已另存为 `TestEvidence/full_suite_run_20261027_1301.log`。
- 组成：DefectHunt 16 条 + 既有与使用场景 375 条（其中使用场景 35 个场景 / 65 例）。
- 12:58–12:59 的“使用场景：…”条目是第二批场景（批量插入、跨页大字段、并发写者、长活读者、事务池等待、五次重启、日志路径占位）的现场数值，均由用例在断言前后写入。

