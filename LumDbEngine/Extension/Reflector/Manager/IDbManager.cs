using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Manager.Specific;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// #if !NATIVE_AOT

namespace LumDbEngine.Element.Manager
{
    internal partial interface IDbManager
    {           

        IDbValue<T> Find
                    <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(DbCache db, string tableName, string keyName, object keyValue) where T : class, new();

         IDbValue<T> FindById<T>(DbCache db, string tableName, uint id) where T : class, new();

        IDbResult Update
                    <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(DbCache db, string tableName, T value, Func<T, bool> condition) where T : class, new();

        IDbResult Update<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(DbCache db, string tableName, uint id, T value) where T : class, new();

        IDbResult Update
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, string tableName, string keyName, object keyValue, T value) where T : class, new();


        IDbValues<T> Find
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, string tableName, Func<T, bool> condition, bool isBackward, uint skip, uint limit) where T : class, new();


        void GoThrough
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (DbCache db, string tableName, Func<T, bool> action) where T : class, new();
    }

}

// #endif