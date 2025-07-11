

# LumDb V1.2.1
- Optimized overall performance.
- fixed thread safety issues during concurrent transaction execution.
- added Readonly transactions to improve parallel efficiency of read-only transactions during concurrency.
- 
# LumDb V1.1.4
- database explorer (simple Winform UI) was added in the project, which could be find also in the github releases.
- bug fixed: in certain situation, insert strVar valueType cause an ArgumentOutOfRangeException.

# LumDb V1.1.3
- fix a insert value bug with inconsistent order or column count.
- add gothrough method to iterate the table data with custom action.

# LumDb V1.1.2
- add inner where and count method with 'limit' and 'skip' parameters.

# LumDb V1.1.1
- The logging system was added to the database to achieve the full ACID principle.
- Remove db.Destory() method, and use db.SetDestoryOnDisposed() insterad.

# LumDb V1.0.8
- Timeout parameter was added in start a transaction during waiting.

# LumDb V1.0.6
- Exception for multi transactions start in single thread has been added to avoid a deadlock.
- The recursive transaction was added to support some data find situation, e.g. called in linq loop search of data.
- Removed the types map in value result, which improved the efficiency.
- Find data in empty table will not cause a exception, but return empty array.
- Detail comments were added.

# LumDb V1.0.2
- LumDb is a single-file database program based on C# for .NET 8. It boasts excellent performance, is 100% written in C#, has no dependencies on external component libraries, and supports AOT (Ahead-of-Time compilation) perfectly.

## Features

- Performance**: LumDb delivers high performance with its efficient design.
- Language**: 100% C# language, ensuring consistency and ease of integration with other C# projects.
- Dependencies**: No external component libraries are required, making it lightweight and easy to deploy.
- AOT Support**: Perfectly supports Ahead-of-Time compilation, enhancing startup performance and reducing runtime overhead.
- Database Structure**: LumDb is a relational database that allows for custom multi-key patterns.
- Data Types**: Supports various data types including int, double, long, bool, decimal, datetime, fixed-length string, variable-length string, fixed-length bytes, and variable-length bytes.
- KV Database Simulation**: Can simulate a KV database and handle file operations based on byte values within tables.
- Thread Safety**: Ensures safe read and write operations across threads.
- Memory-based Transaction Model**: Supports early storage and discard/rollback operations.
- Platform Support**: Currently supports .NET 8 and has been tested on Windows. It is theoretically cross-platform capable.

## Getting Started

To get started with LumDb, please follow these simple steps:

1. **Installation**: Since LumDb is a single-file database, there is no installation process. Simply reference the LumDb library in your .NET 8 project.
2. **Configuration**: Configure your database schema according to your application's needs.
3. **Usage**: Start using LumDb to manage your data with the provided API.

## Contribution

We welcome contributions to LumDb. If you find any issues or have feature requests, please submit them through our issue tracker.

## License

LumDb is licensed under the MIT License. See the [LICENSE](LICENSE.txt) file for more information.

---

Please enjoy using LumDb and help us make it even better by providing feedback and contributions!
