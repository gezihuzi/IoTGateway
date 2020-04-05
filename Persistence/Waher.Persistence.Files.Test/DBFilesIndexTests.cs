﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Waher.Persistence.Serialization;
using Waher.Persistence.Filters;
using Waher.Persistence.Files.Statistics;
using Waher.Script;
using Waher.Script.Objects.Matrices;

#if !LW
using Waher.Persistence.Files.Test.Classes;

namespace Waher.Persistence.Files.Test
#else
using Waher.Persistence.Files;
using Waher.Persistence.FilesLW.Test.Classes;

namespace Waher.Persistence.FilesLW.Test
#endif
{
    [TestClass]
    public abstract class DBFilesIndexTests
    {
        internal const string ObjFileName = "Data\\LastObject.bin";
        internal const string ObjIdFileName = "Data\\LastObjectId.bin";
        internal const string BlockSizeFileName = "Data\\BlockSize.bin";
        internal const int BlocksInCache = 10000;
        internal const int ObjectsToEnumerate = 1000;

        protected ObjectBTreeFile file;
        protected IndexBTreeFile index1;
        protected IndexBTreeFile index2;
        protected IndexBTreeFile index3;
        protected FilesProvider provider;
        protected Random gen = new Random();
        protected DateTime start;

        public abstract int BlockSize
        {
            get;
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            DBFilesBTreeTests.DeleteFiles();

#if LW
            this.provider = new FilesProvider(DBFilesBTreeTests.Folder, DBFilesBTreeTests.CollectionName, BlockSize, BlocksInCache, Math.Max(BlockSize / 2, 1024), Encoding.UTF8, 10000);
#else
            this.provider = new FilesProvider(DBFilesBTreeTests.Folder, DBFilesBTreeTests.CollectionName, BlockSize, BlocksInCache, Math.Max(BlockSize / 2, 1024), Encoding.UTF8, 10000, true);
#endif
            this.file = await this.provider.GetFile(DBFilesBTreeTests.CollectionName);

            this.index1 = await this.provider.GetIndexFile(this.file, RegenerationOptions.DontRegenerate, "Byte", "-DateTime");
            this.index2 = await this.provider.GetIndexFile(this.file, RegenerationOptions.DontRegenerate, "ShortString");
            this.index3 = await this.provider.GetIndexFile(this.file, RegenerationOptions.DontRegenerate, "CIString");

            this.start = DateTime.Now;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Console.Out.WriteLine("Elapsed time: " + (DateTime.Now - this.start).ToString());

            if (this.provider != null)
            {
                this.provider.Dispose();
                this.provider = null;
                this.file = null;
            }
        }

        public virtual int MaxStringLength
        {
            get { return 100; }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_01_NormalEnumeration()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
            SortedDictionary<Guid, bool> Objects3 = new SortedDictionary<Guid, bool>();
            Simple Prev = null;

            foreach (Simple Obj in this.index1)
            {
                Objects2[Obj.ObjectId] = true;
                Objects3[Obj.ObjectId] = true;

                if (Prev != null)
                    AssertEx.Less(this.Index1Compare(Prev, Obj), 0);

                Prev = Obj;
                Assert.IsTrue(Objects.Remove(Obj.ObjectId));
            }

            AssertEx.Same(0, Objects.Count);

            Prev = null;
            foreach (Simple Obj in this.index2)
            {
                if (Prev != null)
                    AssertEx.Less(this.Index2Compare(Prev, Obj), 0);

                Prev = Obj;
                Assert.IsTrue(Objects2.Remove(Obj.ObjectId));
            }

            AssertEx.Same(0, Objects2.Count);

            Prev = null;
            foreach (Simple Obj in this.index3)
            {
                if (Prev != null)
                    AssertEx.Less(this.Index3Compare(Prev, Obj), 0);

                Prev = Obj;
                Assert.IsTrue(Objects3.Remove(Obj.ObjectId));
            }

            AssertEx.Same(0, Objects3.Count);
        }

        private int Index1Compare(GenericObject Obj1, GenericObject Obj2)
        {
            int i = ((IComparable)Obj1["Byte"]).CompareTo(Obj2["Byte"]);
            if (i != 0)
                return i;

            i = ((IComparable)Obj1["DateTime"]).CompareTo(Obj2["DateTime"]);
            if (i != 0)
                return -i;

            return 0;
        }

        private int Index2Compare(GenericObject Obj1, GenericObject Obj2)
        {
            return ((IComparable)Obj1["ShortString"]).CompareTo(Obj2["ShortString"]);
        }

        private int Index3Compare(GenericObject Obj1, GenericObject Obj2)
        {
            return ((IComparable)Obj1["CIString"]).CompareTo(Obj2["CIString"]);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_02_TypedEnumeration()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
            SortedDictionary<Guid, bool> Objects3 = new SortedDictionary<Guid, bool>();
            Simple Prev = null;
            Simple Obj;
            ulong Rank = 0;

            using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(false))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    Objects2[Obj.ObjectId] = true;
                    Objects3[Obj.ObjectId] = true;

                    if (Prev != null)
                        AssertEx.Less(this.Index1Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));

                    AssertEx.Same(Rank++, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects.Count);

            Prev = null;
            Rank = 0;
            using (IndexBTreeFileEnumerator<Simple> e = await this.index2.GetTypedEnumerator<Simple>(false))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Less(this.Index2Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects2.Remove(Obj.ObjectId));

                    AssertEx.Same(Rank++, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects2.Count);

            Prev = null;
            Rank = 0;
            using (IndexBTreeFileEnumerator<Simple> e = await this.index3.GetTypedEnumerator<Simple>(false))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Less(this.Index3Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects3.Remove(Obj.ObjectId));

                    AssertEx.Same(Rank++, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects3.Count);
        }

        private int Index1Compare(Simple Obj1, Simple Obj2)
        {
            int i = Obj1.Byte.CompareTo(Obj2.Byte);
            if (i != 0)
                return i;

            i = Obj1.DateTime.CompareTo(Obj2.DateTime);
            if (i != 0)
                return -i;

            return 0;
        }

        private int Index2Compare(Simple Obj1, Simple Obj2)
        {
            return Obj1.ShortString.CompareTo(Obj2.ShortString);
        }

        private int Index3Compare(Simple Obj1, Simple Obj2)
        {
            return Obj1.CIString.CompareTo(Obj2.CIString);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_03_LockedEnumeration()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
            SortedDictionary<Guid, bool> Objects3 = new SortedDictionary<Guid, bool>();
            Simple Prev = null;
            Simple Obj;
            ulong Rank = 0;

            using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(true))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    Objects2[Obj.ObjectId] = true;
                    Objects3[Obj.ObjectId] = true;

                    if (Prev != null)
                        AssertEx.Less(this.Index1Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));

                    AssertEx.Same(Rank++, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects.Count);

            Prev = null;
            Rank = 0;
            using (IndexBTreeFileEnumerator<Simple> e = await this.index2.GetTypedEnumerator<Simple>(true))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Less(this.Index2Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects2.Remove(Obj.ObjectId));

                    AssertEx.Same(Rank++, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects2.Count);

            Prev = null;
            Rank = 0;
            using (IndexBTreeFileEnumerator<Simple> e = await this.index3.GetTypedEnumerator<Simple>(true))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Less(this.Index3Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects3.Remove(Obj.ObjectId));

                    AssertEx.Same(Rank++, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects3.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(TimeoutException))]
        public async Task DBFiles_Index_Test_04_UnlockedChangeEnumeration()
        {
            await this.CreateObjects(Math.Min(ObjectsToEnumerate, 1000));
            Simple Obj;

            using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(true))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    Obj = DBFilesBTreeTests.CreateSimple(this.MaxStringLength);
                    await this.file.SaveNewObject(Obj);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_05_BackwardsEnumeration()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
            SortedDictionary<Guid, bool> Objects3 = new SortedDictionary<Guid, bool>();
            Simple Prev = null;
            Simple Obj;
            ulong Rank = ObjectsToEnumerate;

            using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(true))
            {
                while (e.MovePrevious())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    Objects2[Obj.ObjectId] = true;
                    Objects3[Obj.ObjectId] = true;
                    if (Prev != null)
                        AssertEx.Greater(this.Index1Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));

                    AssertEx.Same(--Rank, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects.Count);

            Prev = null;
            Rank = ObjectsToEnumerate;
            using (IndexBTreeFileEnumerator<Simple> e = await this.index2.GetTypedEnumerator<Simple>(true))
            {
                while (e.MovePrevious())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Greater(this.Index2Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects2.Remove(Obj.ObjectId));

                    AssertEx.Same(--Rank, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects2.Count);

            Prev = null;
            Rank = ObjectsToEnumerate;
            using (IndexBTreeFileEnumerator<Simple> e = await this.index3.GetTypedEnumerator<Simple>(true))
            {
                while (e.MovePrevious())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Greater(this.Index2Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects3.Remove(Obj.ObjectId));

                    AssertEx.Same(--Rank, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects3.Count);
        }

        private async Task<SortedDictionary<Guid, Simple>> CreateObjects(int NrObjects)
        {
            SortedDictionary<Guid, Simple> Result = new SortedDictionary<Guid, Simple>();

            await this.provider.StartBulk();

            while (NrObjects > 0)
            {
                Simple Obj = DBFilesBTreeTests.CreateSimple(this.MaxStringLength);
                Guid ObjectId = await this.file.SaveNewObject(Obj);
                Result[ObjectId] = Obj;
                NrObjects--;
            }

            await this.provider.EndBulk();

            await DBFilesBTreeTests.AssertConsistent(this.file, this.provider, null, null, true);

            return Result;
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_06_SelectIthObject()
        {
            int c = ObjectsToEnumerate;
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(c);
            SortedDictionary<string, Simple> ObjectsSorted = this.SortObjects(Objects);
            Simple[] Ordered = new Simple[c];
            ObjectsSorted.Values.CopyTo(Ordered, 0);
            Simple Prev;
            Simple Obj;
            Random gen = new Random();
            int i, j;

            for (i = 0; i < c; i++)
            {
                j = 0;
                Prev = null;

                if (i < 10 || (gen.Next(0, 2) == 0 && i <= c - 10))
                {
                    using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(true))
                    {
                        Assert.IsTrue(await e.GoToObject((uint)i));

                        do
                        {
                            Obj = e.Current;
                            Assert.IsNotNull(Obj);
                            if (Prev != null)
                                AssertEx.Less(this.Index1Compare(Prev, Obj), 0);

                            Prev = Obj;
                            DBFilesObjectSerializationTests.AssertEqual(Ordered[i + j], Obj);

                            AssertEx.Same(i + j, e.CurrentRank);
                            AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                        }
                        while (await e.MoveNextAsync() && j++ < 10);
                    }
                }
                else
                {
                    using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(true))
                    {
                        Assert.IsTrue(await e.GoToObject((uint)i));

                        do
                        {
                            Obj = e.Current;
                            Assert.IsNotNull(Obj);
                            if (Prev != null)
                                AssertEx.Greater(this.Index1Compare(Prev, Obj), 0);

                            Prev = Obj;
                            DBFilesObjectSerializationTests.AssertEqual(Ordered[i - j], Obj);

                            AssertEx.Same(i - j, e.CurrentRank);
                            AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                        }
                        while (e.MovePrevious() && j++ < 10);
                    }
                }
            }
        }

        private SortedDictionary<string, Simple> SortObjects(SortedDictionary<Guid, Simple> Objects)
        {
            SortedDictionary<string, Simple> ObjectsSorted = new SortedDictionary<string, Simple>();

            foreach (Simple Obj in Objects.Values)
                ObjectsSorted[Obj.Byte.ToString("D3") + " " + (long.MaxValue - Obj.DateTime.Ticks).ToString()] = Obj;

            return ObjectsSorted;
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_07_RankObject()
        {
            int c = ObjectsToEnumerate;
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(c);
            SortedDictionary<string, Simple> ObjectsSorted = this.SortObjects(Objects);
            Simple[] Ordered = new Simple[c];
            ObjectsSorted.Values.CopyTo(Ordered, 0);
            int i;

            for (i = 0; i < c; i++)
                AssertEx.Same(i, await this.index1.GetRank(Ordered[i].ObjectId));
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_08_Reset()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Prev = null;
            Simple Obj;
            ulong Rank = 0;

            using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(false))
            {
                while (await e.MoveNextAsync())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Less(this.Index1Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects.ContainsKey(Obj.ObjectId));

                    AssertEx.Same(Rank++, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }

                e.Reset();
                Prev = null;

                while (e.MovePrevious())
                {
                    Obj = e.Current;
                    Assert.IsNotNull(Obj);
                    if (Prev != null)
                        AssertEx.Greater(this.Index1Compare(Prev, Obj), 0);

                    Prev = Obj;
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));

                    AssertEx.Same(--Rank, e.CurrentRank);
                    AssertEx.Same(Obj.ObjectId, e.CurrentObjectId);
                }
            }

            AssertEx.Same(0, Objects.Count);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_09_UpdateObjects_1000()
        {
            await this.DBFiles_Index_Test_UpdateObjects(1000);
        }

        [TestMethod]
        [Ignore]
        public async Task DBFiles_Index_Test_10_UpdateObjects_10000()
        {
            await this.DBFiles_Index_Test_UpdateObjects(10000);
        }

        [TestMethod]
        [Ignore]
        public async Task DBFiles_Index_Test_11_UpdateObjects_100000()
        {
            await this.DBFiles_Index_Test_UpdateObjects(100000);
        }

        private async Task DBFiles_Index_Test_UpdateObjects(int c)
        {
            Dictionary<Guid, Simple> Ordered = new Dictionary<Guid, Simple>();
            Simple[] Objects = new Simple[c];
            Simple Obj;
            int i;

            for (i = 0; i < c; i++)
            {
                Objects[i] = Obj = DBFilesBTreeTests.CreateSimple(this.MaxStringLength);
                await this.file.SaveNewObject(Obj);
                Ordered[Obj.ObjectId] = Obj;
            }

            await DBFilesBTreeTests.AssertConsistent(this.file, this.provider, null, null, true);

            for (i = 0; i < c; i++)
            {
                Obj = DBFilesBTreeTests.CreateSimple(this.MaxStringLength);
                Obj.ObjectId = Objects[i].ObjectId;

                await this.file.UpdateObject(Obj);

                Objects[i] = Obj;

                Obj = await this.file.LoadObject<Simple>(Obj.ObjectId);
                DBFilesObjectSerializationTests.AssertEqual(Objects[i], Obj);
            }

            await DBFilesBTreeTests.AssertConsistent(this.file, this.provider, null, null, true);

            for (i = 0; i < c; i++)
            {
                Obj = await this.file.LoadObject<Simple>(Objects[i].ObjectId);
                DBFilesObjectSerializationTests.AssertEqual(Objects[i], Obj);
            }

            using (IndexBTreeFileEnumerator<Simple> e = await this.index1.GetTypedEnumerator<Simple>(true))
            {
                Obj = null;

                while (await e.MoveNextAsync())
                {
                    AssertEx.Same(e.Current.ObjectId, e.CurrentObjectId);
                    Assert.IsTrue(Ordered.Remove(e.Current.ObjectId));

                    if (Obj != null)
                        AssertEx.Less(this.Index1Compare(Obj, e.Current), 0);

                    Obj = e.Current;
                }
            }

            AssertEx.Same(0, Ordered.Count);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_12_DeleteObject()
        {
            await this.DBFiles_Index_Test_DeleteObjects(3);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_13_DeleteObject_100()
        {
            await this.DBFiles_Index_Test_DeleteObjects(100);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_14_DeleteObject_1000()
        {
            await this.DBFiles_Index_Test_DeleteObjects(1000);
        }

        [TestMethod]
        [Ignore]
        public async Task DBFiles_Index_Test_15_DeleteObject_10000()
        {
            await this.DBFiles_Index_Test_DeleteObjects(10000);
        }

        [TestMethod]
        [Ignore]
        public async Task DBFiles_Index_Test_16_DeleteObject_100000()
        {
            await this.DBFiles_Index_Test_DeleteObjects(100000);
        }

        private async Task DBFiles_Index_Test_DeleteObjects(int c)
        {
            Random Gen = new Random();
            Simple[] Objects = new Simple[c];
            Simple Obj;
            int i;

            for (i = 0; i < c; i++)
            {
                Objects[i] = Obj = DBFilesBTreeTests.CreateSimple(this.MaxStringLength);
                await this.file.SaveNewObject(Obj);
            }

            while (c > 0)
            {
                i = Gen.Next(0, c);

                Obj = Objects[i];
                c--;
                if (i < c)
                    Array.Copy(Objects, i + 1, Objects, i, c - i);

                await this.file.DeleteObject(Obj);
            }

            FileStatistics Stat = await DBFilesBTreeTests.AssertConsistent(this.file, this.provider, null, null, true);

            AssertEx.Same(0, this.file.Count);
            AssertEx.Same(0, this.index1.IndexFile.Count);
            AssertEx.Same(1, Stat.NrBlocks);
            AssertEx.Same(0, Stat.NrBlobBlocks);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_17_Clear()
        {
            Simple Obj = DBFilesBTreeTests.CreateSimple(this.MaxStringLength);
            Guid ObjectId = await this.file.SaveNewObject(Obj);
            AssertEx.NotSame(Guid.Empty, ObjectId);
            Assert.IsTrue(this.file.Contains(Obj));
            this.file.Clear();
            Assert.IsFalse(this.file.Contains(Obj));
            AssertEx.Same(0, this.file.Count);
            AssertEx.Same(0, this.index1.IndexFile.Count);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_18_FindFirst()
        {
            await this.CreateObjects(ObjectsToEnumerate);
            int i;

            for (i = 0; i < 256; i++)
            {
                using (IndexBTreeFileEnumerator<Simple> e = await this.index1.FindFirstGreaterOrEqualTo<Simple>(true,
                    new KeyValuePair<string, object>("Byte", i)))
                {
                    while (await e.MoveNextAsync())
                    {
                        AssertEx.GreaterOrEqual(e.Current.Byte, i);
                        if (e.Current.Byte > i)
                            break;
                    }

                    e.Reset();

                    while (e.MovePrevious())
                    {
                        AssertEx.Less(e.Current.Byte, i);
                        if (e.Current.Byte < i - 1)
                            break;
                    }
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_19_FindLast()
        {
            await this.CreateObjects(ObjectsToEnumerate);
            int i;

            for (i = 0; i < 256; i++)
            {
                using (IndexBTreeFileEnumerator<Simple> e = await this.index1.FindLastLesserOrEqualTo<Simple>(true,
                    new KeyValuePair<string, object>("Byte", i)))
                {
                    while (e.MovePrevious())
                    {
                        AssertEx.LessOrEqual(e.Current.Byte, i);
                        if (e.Current.Byte < i)
                            break;
                    }

                    e.Reset();

                    while (await e.MoveNextAsync())
                    {
                        AssertEx.Greater(e.Current.Byte, i);
                        if (e.Current.Byte > i + 1)
                            break;
                    }
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_20_Search_FilterEqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldEqualTo("Byte", 100), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Same(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.NotSame(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_21_Search_FilterNotEqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldNotEqualTo("Byte", 100), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.NotSame(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.Same(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_22_Search_FilterGreaterThanOrEqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldGreaterOrEqualTo("Byte", 100), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.GreaterOrEqual(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.Less(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_23_Search_FilterLesserThanOrEqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldLesserOrEqualTo("Byte", 100), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.LessOrEqual(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.Greater(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_24_Search_FilterGreaterThan()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldGreaterThan("Byte", 100), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Greater(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.LessOrEqual(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_25_Search_FilterLesserThan()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldLesserThan("Byte", 100), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Less(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.GreaterOrEqual(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_26_Search_FilterLikeRegEx()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldLikeRegEx("ShortString", "A.*B.*"), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.ShortString.StartsWith("A"));
                    Assert.IsTrue(Obj.ShortString.Contains("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse(Obj2.ShortString.StartsWith("A") && Obj2.ShortString.Contains("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_27_Search_FilterLikeRegEx_2()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldLikeRegEx("ShortString", "[AB].*"), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.ShortString.StartsWith("A") || Obj.ShortString.StartsWith("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse(Obj2.ShortString.StartsWith("A") || Obj2.ShortString.StartsWith("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_28_Search_FilterNot_EqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldEqualTo("Byte", 100)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.NotSame(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.Same(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_29_Search_FilterNot_NotEqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldNotEqualTo("Byte", 100)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Same(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.NotSame(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_30_Search_FilterNot_GreaterThanOrEqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldGreaterOrEqualTo("Byte", 100)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Less(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.GreaterOrEqual(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_31_Search_FilterNot_LesserThanOrEqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldLesserOrEqualTo("Byte", 100)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.GreaterOrEqual(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.LessOrEqual(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_32_Search_FilterNot_GreaterThan()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldGreaterThan("Byte", 100)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.LessOrEqual(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.Greater(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_33_Search_FilterNot_LesserThan()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldLesserThan("Byte", 100)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.GreaterOrEqual(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.Less(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_34_Search_FilterNot_LikeRegEx()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldLikeRegEx("ShortString", "A.*B.*")), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsFalse(Obj.ShortString.StartsWith("A") && Obj.ShortString.Contains("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsTrue(Obj2.ShortString.StartsWith("A") && Obj2.ShortString.Contains("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_35_Search_FilterNot_LikeRegEx_2()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldLikeRegEx("ShortString", "[AB].*")), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsFalse(Obj.ShortString.StartsWith("A") || Obj.ShortString.StartsWith("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsTrue(Obj2.ShortString.StartsWith("A") || Obj2.ShortString.StartsWith("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_36_Search_FilterNot_Not_EqualTo()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterNot(new FilterFieldEqualTo("Byte", 100))), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Same(Obj.Byte, 100);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    AssertEx.NotSame(Obj2.Byte, 100);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_37_Search_FilterOr()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterOr(
                new FilterFieldEqualTo("Byte", 100),
                new FilterFieldEqualTo("Byte", 200),
                new FilterFieldLikeRegEx("ShortString", "A.*")), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.Byte == 100 || Obj.Byte == 200 || Obj.ShortString.StartsWith("A"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse(Obj2.Byte == 100 || Obj2.Byte == 200 || Obj2.ShortString.StartsWith("A"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_38_Search_OneRange_Closed()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterOrEqualTo("Byte", 100),
                new FilterFieldLesserOrEqualTo("Byte", 200)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.GreaterOrEqual(Obj.Byte, 100);
                    AssertEx.LessOrEqual(Obj.Byte, 200);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsTrue(Obj2.Byte < 100 || Obj2.Byte > 200);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_39_Search_OneRange_Open()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("Byte", 100),
                new FilterFieldLesserThan("Byte", 200)), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Greater(Obj.Byte, 100);
                    AssertEx.Less(Obj.Byte, 200);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsTrue(Obj2.Byte <= 100 || Obj2.Byte >= 200);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_40_Search_TwoRanges_Closed()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            //SortedDictionary<Guid, Simple> Objects = await this.TestDB();
            Simple Obj;
            bool b;
            int NrFalsePositives = 0;
            int NrFalseNegatives = 0;
            int RecNr = 0;

            Database.Register(this.provider);
            Expression Exp = new Expression("select Byte, DateTime from Simple");
            Variables v = new Variables();
            ObjectMatrix Ans = Exp.Evaluate(v) as ObjectMatrix;
            StringBuilder sb = new StringBuilder();
            int Rows = Ans.Rows;
            int Row;

            sb.AppendLine("Simple Obj;");
            sb.AppendLine("Guid ObjectId;");
            sb.AppendLine("SortedDictionary<Guid, Simple> Result = new SortedDictionary<Guid, Simple>();");
            sb.AppendLine("await this.provider.StartBulk();");

            for (Row = 0; Row < Rows; Row++)
            {
                DateTime TP = (DateTime)Ans.GetElement(1, Row).AssociatedObjectValue;
                sb.AppendLine("Obj=new Simple() { Byte=" + Ans.GetElement(0, Row).ToString() + ", DateTime=new DateTime(" + TP.Year + ", " + TP.Month + ", " + TP.Day + ", " + TP.Hour + ", " + TP.Minute + ", " + TP.Second + ", " + TP.Millisecond + ") };");
                sb.AppendLine("ObjectId = await this.file.SaveNewObject(Obj);");
                sb.AppendLine("Result[ObjectId] = Obj;");
            }

            sb.AppendLine("await this.provider.EndBulk();");

            Console.Out.WriteLine(sb.ToString());

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterOrEqualTo("Byte", 100),
                new FilterFieldLesserOrEqualTo("Byte", 200),
                new FilterFieldGreaterOrEqualTo("DateTime", MinDT),
                new FilterFieldLesserOrEqualTo("DateTime", MaxDT)), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    RecNr++;

                    try
                    {
                        Obj = Cursor.Current;
                        Assert.IsNotNull(Obj);
                        AssertEx.GreaterOrEqual(Obj.Byte, 100);
                        AssertEx.LessOrEqual(Obj.Byte, 200);
                        AssertEx.GreaterOrEqual(Obj.DateTime, MinDT);
                        AssertEx.LessOrEqual(Obj.DateTime, MaxDT);
                        Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                    }
                    catch (AssertFailedException)
                    {
                        NrFalsePositives++;
                    }
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte < 100 || Obj2.Byte > 200 || Obj2.DateTime < MinDT || Obj2.DateTime > MaxDT;
                    if (!b)
                        NrFalseNegatives++;
                }
            }

            Assert.AreEqual(0, NrFalsePositives);
            Assert.AreEqual(0, NrFalseNegatives);
        }

        /*private async Task<SortedDictionary<Guid, Simple>> TestDB()
        {
            Simple Obj;
            Guid ObjectId;
            SortedDictionary<Guid, Simple> Result = new SortedDictionary<Guid, Simple>();

            await this.provider.EndBulk();
            return Result;
        }*/

        private static readonly DateTime MinDT = new DateTime(1950, 6, 7, 8, 9, 10);
        private static readonly DateTime MaxDT = new DateTime(1990, 2, 3, 4, 5, 6);

        [TestMethod]
        public async Task DBFiles_Index_Test_41_Search_TwoRanges_Open()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("Byte", 100),
                new FilterFieldLesserThan("Byte", 200),
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT)), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Greater(Obj.Byte, 100);
                    AssertEx.Less(Obj.Byte, 200);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte <= 100 || Obj2.Byte >= 200 || Obj2.DateTime <= MinDT || Obj2.DateTime >= MaxDT;
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_42_Search_TwoRanges_Open_AdditionalFilter()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("Byte", 100),
                new FilterFieldLesserThan("Byte", 200),
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterFieldLikeRegEx("ShortString", "[A-Z].*")), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Greater(Obj.Byte, 100);
                    AssertEx.Less(Obj.Byte, 200);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(Obj.ShortString[0] >= 'A' && Obj.ShortString[0] <= 'Z');
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte <= 100 || Obj2.Byte >= 200 || Obj2.DateTime <= MinDT || Obj2.DateTime >= MaxDT || Obj2.ShortString[0] < 'A' || Obj2.ShortString[0] > 'Z';
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_43_Search_TwoOpenRanges_MinMin()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("Byte", 100),
                new FilterFieldGreaterThan("DateTime", MinDT)), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Greater(Obj.Byte, 100);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte <= 100 || Obj2.DateTime <= MinDT;
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_44_Search_TwoOpenRanges_MinMax()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("Byte", 100),
                new FilterFieldLesserThan("DateTime", MaxDT)), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Greater(Obj.Byte, 100);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte <= 100 || Obj2.DateTime >= MaxDT;
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_45_Search_TwoOpenRanges_MaxMin()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldLesserThan("Byte", 200),
                new FilterFieldGreaterThan("DateTime", MinDT)), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Less(Obj.Byte, 200);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte >= 200 || Obj2.DateTime <= MinDT;
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_46_Search_TwoOpenRanges_MaxMax()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldLesserThan("Byte", 200),
                new FilterFieldLesserThan("DateTime", MaxDT)), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Less(Obj.Byte, 200);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte >= 200 || Obj2.DateTime >= MaxDT;
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_47_Search_TwoRanges_Normalization()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterOr(
                    new FilterFieldGreaterThan("Byte", 200),
                    new FilterFieldLesserThan("Byte", 100))), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.Byte < 100 || Obj.Byte > 200);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.DateTime <= MinDT || Obj2.DateTime >= MaxDT || (Obj2.Byte >= 100 && Obj2.Byte <= 200);
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_48_Search_Or_AvoidMultipleFullFileScans()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            FileStatistics StatBefore = (await this.file.ComputeStatistics()).Key;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterOr(
                new FilterFieldLikeRegEx("ShortString", "[AB].*"),
                new FilterFieldLikeRegEx("ShortString", "[XY].*")), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue("ABXY".IndexOf(Obj.ShortString[0]) >= 0);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse("ABXY".IndexOf(Obj2.ShortString[0]) >= 0);
            }

            FileStatistics StatAfter = (await this.file.ComputeStatistics()).Key;
            ulong NrFullFileScans = StatAfter.NrFullFileScans - StatBefore.NrFullFileScans;
            AssertEx.Same(1, NrFullFileScans);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_49_Search_SortOrder()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Prev = null;
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterOr(
                    new FilterFieldGreaterThan("Byte", 200),
                    new FilterFieldLesserThan("Byte", 100))), true, "SByte", "ShortString"))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.Byte < 100 || Obj.Byte > 200);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));

                    if (Prev != null)
                    {
                        Assert.IsTrue(Prev.SByte < Obj.SByte ||
                            (Prev.SByte == Obj.SByte && string.Compare(Prev.ShortString, Obj.ShortString) <= 0));
                    }

                    Prev = Obj;
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.DateTime <= MinDT || Obj2.DateTime >= MaxDT || (Obj2.Byte >= 100 && Obj2.Byte <= 200);
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_50_Search_Paging()
        {
            await this.CreateObjects(ObjectsToEnumerate);
            List<Simple> Ordered = new List<Simple>();
            int i;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterOr(
                    new FilterFieldGreaterThan("Byte", 200),
                    new FilterFieldLesserThan("Byte", 100))), true, "SByte", "ShortString"))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Ordered.Add(Cursor.Current);
                }
            }

            i = 20;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(20, 10, new FilterAnd(
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterOr(
                    new FilterFieldGreaterThan("Byte", 200),
                    new FilterFieldLesserThan("Byte", 100))), true, "SByte", "ShortString"))
            {
                while (await Cursor.MoveNextAsync())
                {
                    DBFilesObjectSerializationTests.AssertEqual(Ordered[i++], Cursor.Current);
                }
            }

            AssertEx.LessOrEqual(i, 40);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_51_Search_DefaultValue()
        {
            SortedDictionary<Guid, Default> Objects = await this.CreateDefaultObjects(ObjectsToEnumerate);

            using (ICursor<Default> Cursor = await this.file.Find<Default>(0, int.MaxValue, new FilterFieldLesserOrEqualTo("Byte", 10), true))
            {
                Default Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.LessOrEqual(Obj.Byte, 10);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Default Obj2 in Objects.Values)
                    AssertEx.Greater(Obj2.Byte, 10);
            }
        }

        private async Task<SortedDictionary<Guid, Default>> CreateDefaultObjects(int NrObjects)
        {
            SortedDictionary<Guid, Default> Result = new SortedDictionary<Guid, Default>();

            while (NrObjects > 0)
            {
                Default Obj = DBFilesBTreeTests.CreateDefault(this.MaxStringLength);
                Guid ObjectId = await this.file.SaveNewObject(Obj);
                Result[ObjectId] = Obj;
                NrObjects--;
            }

            await DBFilesBTreeTests.AssertConsistent(this.file, this.provider, null, null, true);

            return Result;
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_52_Search_MixedTypes()
        {
            SortedDictionary<Guid, Default> Objects1 = await this.CreateDefaultObjects(ObjectsToEnumerate);
            SortedDictionary<Guid, Simple> Objects2 = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Default> Cursor = await this.file.Find<Default>(0, int.MaxValue, new FilterFieldLesserOrEqualTo("Byte", 10), true))
            {
                Default Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.LessOrEqual(Obj.Byte, 10);
                    Assert.IsTrue(Objects1.Remove(Obj.ObjectId));
                }

                foreach (Default Obj2 in Objects1.Values)
                    AssertEx.Greater(Obj2.Byte, 10);
            }

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldLesserOrEqualTo("Byte", 10), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.LessOrEqual(Obj.Byte, 10);
                    Assert.IsTrue(Objects2.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects2.Values)
                    AssertEx.Greater(Obj2.Byte, 10);
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_53_Search_FilterLikeRegEx_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldLikeRegEx("CIString", "A.*B.*"), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.CIString.StartsWith("A"));
                    Assert.IsTrue(Obj.CIString.Contains("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse(Obj2.CIString.StartsWith("A") && Obj2.CIString.Contains("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_54_Search_FilterLikeRegEx_2_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterFieldLikeRegEx("CIString", "[AB].*"), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.CIString.StartsWith("A") || Obj.CIString.StartsWith("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse(Obj2.CIString.StartsWith("A") || Obj2.CIString.StartsWith("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_55_Search_FilterNot_LikeRegEx_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue,
                new FilterNot(new FilterFieldLikeRegEx("CIString", "A.*B.*")), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsFalse(Obj.CIString.StartsWith("A") && Obj.CIString.Contains("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsTrue(Obj2.CIString.StartsWith("A") && Obj2.CIString.Contains("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_56_Search_FilterNot_LikeRegEx_2_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterNot(new FilterFieldLikeRegEx("CIString", "[AB].*")), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsFalse(Obj.CIString.StartsWith("A") || Obj.CIString.StartsWith("B"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsTrue(Obj2.CIString.StartsWith("A") || Obj2.CIString.StartsWith("B"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_57_Search_FilterOr_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterOr(
                new FilterFieldEqualTo("Byte", 100),
                new FilterFieldEqualTo("Byte", 200),
                new FilterFieldLikeRegEx("CIString", "A.*")), true))
            {
                Simple Obj;

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.Byte == 100 || Obj.Byte == 200 || Obj.CIString.StartsWith("A"));
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse(Obj2.Byte == 100 || Obj2.Byte == 200 || Obj2.CIString.StartsWith("A"));
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_58_Search_TwoRanges_Open_AdditionalFilter_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("Byte", 100),
                new FilterFieldLesserThan("Byte", 200),
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterFieldLikeRegEx("CIString", "[A-Z].*")), true))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    AssertEx.Greater(Obj.Byte, 100);
                    AssertEx.Less(Obj.Byte, 200);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(char.ToUpper(Obj.CIString[0]) >= 'A' &&
                        char.ToUpper(Obj.CIString[0]) <= 'Z');
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.Byte <= 100 || Obj2.Byte >= 200 || Obj2.DateTime <= MinDT || Obj2.DateTime >= MaxDT || Obj2.CIString[0] < 'A' || Obj2.CIString[0] > 'Z';
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_59_Search_Or_AvoidMultipleFullFileScans_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            FileStatistics StatBefore = (await this.file.ComputeStatistics()).Key;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterOr(
                new FilterFieldLikeRegEx("CIString", "[AB].*"),
                new FilterFieldLikeRegEx("CIString", "[XY].*")), true))
            {
                Simple Obj;
                CaseInsensitiveString s = "ABXY";

                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(s.IndexOf(Obj.CIString[0]) >= 0);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));
                }

                foreach (Simple Obj2 in Objects.Values)
                    Assert.IsFalse("ABXY".IndexOf(Obj2.CIString[0]) >= 0);
            }

            FileStatistics StatAfter = (await this.file.ComputeStatistics()).Key;
            ulong NrFullFileScans = StatAfter.NrFullFileScans - StatBefore.NrFullFileScans;
            AssertEx.Same(1, NrFullFileScans);
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_60_Search_SortOrder_CaseInsensitive()
        {
            SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
            Simple Prev = null;
            Simple Obj;
            bool b;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterOr(
                    new FilterFieldGreaterThan("Byte", 200),
                    new FilterFieldLesserThan("Byte", 100))), true, "SByte", "CIString"))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Obj = Cursor.Current;
                    Assert.IsNotNull(Obj);
                    Assert.IsTrue(Obj.Byte < 100 || Obj.Byte > 200);
                    AssertEx.Greater(Obj.DateTime, MinDT);
                    AssertEx.Less(Obj.DateTime, MaxDT);
                    Assert.IsTrue(Objects.Remove(Obj.ObjectId));

                    if (Prev != null)
                    {
                        Assert.IsTrue(Prev.SByte < Obj.SByte ||
                            (Prev.SByte == Obj.SByte && string.Compare(Prev.CIString, Obj.CIString) <= 0));
                    }

                    Prev = Obj;
                }

                foreach (Simple Obj2 in Objects.Values)
                {
                    b = Obj2.DateTime <= MinDT || Obj2.DateTime >= MaxDT || (Obj2.Byte >= 100 && Obj2.Byte <= 200);
                    Assert.IsTrue(b);
                }
            }
        }

        [TestMethod]
        public async Task DBFiles_Index_Test_61_Search_Paging_CaseInsensitive()
        {
            await this.CreateObjects(ObjectsToEnumerate);
            List<Simple> Ordered = new List<Simple>();
            int i;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, int.MaxValue, new FilterAnd(
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterOr(
                    new FilterFieldGreaterThan("Byte", 200),
                    new FilterFieldLesserThan("Byte", 100))), true, "SByte", "CIString"))
            {
                while (await Cursor.MoveNextAsync())
                {
                    Ordered.Add(Cursor.Current);
                }
            }

            i = 20;

            using (ICursor<Simple> Cursor = await this.file.Find<Simple>(20, 10, new FilterAnd(
                new FilterFieldGreaterThan("DateTime", MinDT),
                new FilterFieldLesserThan("DateTime", MaxDT),
                new FilterOr(
                    new FilterFieldGreaterThan("Byte", 200),
                    new FilterFieldLesserThan("Byte", 100))), true, "SByte", "CIString"))
            {
                while (await Cursor.MoveNextAsync())
                {
                    DBFilesObjectSerializationTests.AssertEqual(Ordered[i++], Cursor.Current);
                }
            }

            AssertEx.LessOrEqual(i, 40);
        }

    }
}
