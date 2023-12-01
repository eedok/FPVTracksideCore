﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaceLib
{
    public interface IDatabase : IDisposable
    {
        int Version { get; }

        Club GetDefaultClub();
        IEnumerable<Event> GetEvents();
        Event LoadEvent(Guid eventId);
        IEnumerable<Race> LoadRaces(Guid eventId);
        IEnumerable<Result> LoadResults(Guid eventId);

        bool Insert<T>(T t) where T : BaseObject, new();
        int Insert<T>(IEnumerable<T> t) where T : BaseObject, new();
        
        bool Update<T>(T t) where T : BaseObject, new();
        int Update<T>(IEnumerable<T> t) where T : BaseObject, new();
        
        bool Upsert<T>(T t) where T : BaseObject, new();
        int Upsert<T>(IEnumerable<T> t) where T : BaseObject, new();

        bool Delete<T>(T t) where T : BaseObject, new();
        int Delete<T>(IEnumerable<T> t) where T : BaseObject, new();

        IEnumerable<T> All<T>() where T : BaseObject, new();

        T GetCreateExternalObject<T>(int externalId) where T : BaseObject, new();
        T GetCreateObject<T>(Guid id) where T : BaseObject, new();

        T GetObject<T>(Guid id) where T : BaseObject, new();
        IEnumerable<T> GetObjects<T>(IEnumerable<Guid> ids) where T : BaseObject, new();
    }

    public interface IDatabaseFactory
    {
        IDatabase Open();
    }

    public static class DatabaseFactory
    {
        private static IDatabaseFactory databaseFactory;

        public static void Init(IDatabaseFactory dbf)
        {
            databaseFactory = dbf;
        }

        public static IDatabase Open() 
        { 
            return databaseFactory.Open();
        }

    }
}
