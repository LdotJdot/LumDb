

## LumDb V1.3.1

## Demo

```csharp
using LumDb;

public record Student(string Name, int Age);

class Program
{
    public class Student
    {
        public Student()
        {

        }

        public Student(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; set; } = "";
        public int Age { get; set; } = 0;
    }

    static void Main()
    {
          // 1. Create database file and tables
        
           using (DbEngine engineCreate = new DbEngine("d:\\xxxxReflectorInsert.db", true))
            {
                engineCreate.TimeoutMilliseconds = 10000; // set timeout to 10 seconds

                using (var tsCreate = engineCreate.StartTransaction(0, false))
                {
                    tsCreate.Create<Student>(TableNameFirst);
                    tsCreate.Create<StudentInfo>(TableNameSecond);
                }
            }

            // 2. Open existing database and insert 100 records
            using DbEngine engine = new DbEngine("d:\\xxxxReflectorInsert.db");
            using (ITransaction transaction = engine.StartTransaction())
            {
                for (int index = 0; index < 100; index++)
                {
                    transaction.Insert(TableNameFirst, new Student("lj" + index.ToString(), index));
                    transaction.Insert(TableNameSecond, new StudentInfo(index.ToString() + "lj", index));
                }

                // ts.Dispose();  // manually committed.
                    // ts.Discard();
            } // transaction will be auto committed

            // 3. Query data
            using (ITransaction transactionQuery = engine.StartTransaction())
            {
                var result1 = transactionQuery.Find<Student>(TableNameFirst, o => o.Age > 98);

                foreach (var value in result1.Values)
                {
                    Console.WriteLine(value.Name + " " + value.Age.ToString());
                }

                var result2 = transactionQuery.Find(TableNameFirst, ("Age", o => ((int)o) < 2));

                foreach (var value in result2.Values)
                {
                    Console.WriteLine(value[0].ToString() + " " + value[1].ToString());
                }

                var result3 = transactionQuery.Find<StudentInfo>(TableNameSecond, o => o.Age % 17 == 0);

                foreach (var value in result3.Values)
                {
                    Console.WriteLine($"{value.Id}, {value.Name},{value.Age}");
                }
            }

            engine.SetDestoryOnDisposed();

            Console.WriteLine("All complete");
     }
}

Outputs:
lj99 99
lj0 0
lj1 1
1, 0lj,0
18, 17lj,17
35, 34lj,34
52, 51lj,51
69, 68lj,68
86, 85lj,85
All complete


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
