﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using SharpFileDB.Blocks;
using SharpFileDB.Utilities;
using System.Reflection;
using System.Linq.Expressions;

namespace SharpFileDB
{
    /// <summary>
    /// 单文件数据库上下文，代表一个单文件数据库。SharpFileDB的核心类型。
    /// </summary>
    public partial class FileDBContext : IDisposable
    {

        /// <summary>
        /// 单文件数据库上下文，代表一个单文件数据库。SharpFileDB的核心类型。
        /// </summary>
        /// <param name="fullname">数据库文件据对路径。</param>
        /// <param name="config">数据库配置信息。只有在新建数据库时此参数才会发挥作用。</param>
        /// <param name="onlyRead">只读方式打开。</param>
        public FileDBContext(string fullname, DBConfig config = null, bool onlyRead = false)
        {
            this.transaction = new Transaction(this);

            this.Fullname = fullname;

            if (!onlyRead)
            {
                if (!File.Exists(fullname))
                {
                    if (config == null) { config = new DBConfig(); }
                    CreateDB(fullname, config);
                }

                InitializeDB(fullname, onlyRead);
            }
            else
            {
                InitializeDB(fullname, onlyRead);
            }
        }

        /// <summary>
        /// 根据数据库文件初始化<see cref="FileDBContext"/>。
        /// </summary>
        /// <param name="fullname">数据库文件据对路径。</param>
        /// <param name="read">只读方式打开。</param>
        private void InitializeDB(string fullname, bool read)
        {
            // 尝试恢复数据库文件。
            string journalFilename = this.JournalFileName;
            if (File.Exists(journalFilename))
            {
                using (FileStream journal = new FileStream(journalFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using (FileStream fs = new FileStream(fullname, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        while (journal.Position < journal.Length)
                        {
                            object obj = Consts.formatter.Deserialize(journal);
                            Block block = obj as Block;
                            fs.WriteBlock(block);
                        }
                    }
                }
                File.Delete(journalFilename);
            }

            // 准备各项工作。
            // 准备数据库文件流。
            FileStream fileStream = null;
            if (read)
            {
                fileStream = new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                this.fileStream = fileStream;
            }
            else
            {
                fileStream = new FileStream(fullname, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                this.fileStream = fileStream;
            }
            // 准备数据库头部块。
            //PageHeaderBlock pageHeaderBlock = fileStream.ReadBlock<PageHeaderBlock>(0);
            DBHeaderBlock headerBlock = fileStream.ReadBlock<DBHeaderBlock>(Consts.pageHeaderBlockLength);
#if DEBUG
            Block.IDCounter = headerBlock.BlockCount;
#endif
            BlockCache.MaxSunkCountInMemory = headerBlock.MaxSunkCountInMemory;
            this.headerBlock = headerBlock;
            // 准备数据库表块，保存到字典。
            TableBlock currentTableBlock = fileStream.ReadBlock<TableBlock>(Consts.pageHeaderBlockLength + Consts.dbHeaderBlockLength); 
            this.tableBlockHead = currentTableBlock;
            while (currentTableBlock.NextPos != 0)
            {
                TableBlock tableBlock = fileStream.ReadBlock<TableBlock>(currentTableBlock.NextPos);

                currentTableBlock.NextObj = tableBlock;

                Dictionary<string, IndexBlock> indexDict = GetIndexDict(fileStream, tableBlock);
                this.tableIndexBlockDict.Add(tableBlock.TableType, indexDict);

                this.tableBlockDict.Add(tableBlock.TableType, tableBlock);

                currentTableBlock = tableBlock;
            }
        }

        private Dictionary<string, IndexBlock> GetIndexDict(FileStream fileStream, TableBlock tableBlock)
        {
            Dictionary<string, IndexBlock> indexDict = new Dictionary<string, IndexBlock>();

            long indexBlockHeadPos = tableBlock.IndexBlockHeadPos;
            IndexBlock currentIndexBlock = fileStream.ReadBlock<IndexBlock>(indexBlockHeadPos);
            tableBlock.IndexBlockHead = currentIndexBlock;

            currentIndexBlock.TryLoadNextObj(fileStream);// 此时的currentIndexBlock是索引块的头结点。
            while (currentIndexBlock.NextObj != null)
            {
                SkipListNodeBlock[] headNodes = GetHeadNodesOfSkipListNodeBlock(fileStream, currentIndexBlock.NextObj);
                currentIndexBlock.NextObj.SkipListHeadNodes = headNodes;
                SkipListNodeBlock tailNode = GetTailNodeOfSkipListNodeBlock(fileStream, currentIndexBlock.NextObj);
                currentIndexBlock.NextObj.SkipListTailNode = tailNode;
                foreach (SkipListNodeBlock headNode in currentIndexBlock.NextObj.SkipListHeadNodes)
                {
                    if (headNode.RightPos == tailNode.ThisPos)
                    { headNode.RightObj = tailNode; }
                }

                currentIndexBlock.NextObj.TryLoadNextObj(fileStream);

                indexDict.Add(currentIndexBlock.NextObj.BindMember, currentIndexBlock.NextObj);

                currentIndexBlock = currentIndexBlock.NextObj;
            }
            return indexDict;
        }

        private SkipListNodeBlock GetTailNodeOfSkipListNodeBlock(FileStream fileStream, IndexBlock indexBlock)
        {
            long currentSkipListNodeBlockPos = indexBlock.SkipListTailNodePos;
            if (currentSkipListNodeBlockPos == 0)
            { throw new Exception(string.Format("tail node not set for [{0}]", indexBlock)); }

            SkipListNodeBlock tailNode = fileStream.ReadBlock<SkipListNodeBlock>(currentSkipListNodeBlockPos);

            return tailNode;
        }

        private SkipListNodeBlock[] GetHeadNodesOfSkipListNodeBlock(FileStream fileStream, IndexBlock indexBlock)
        {
            SkipListNodeBlock[] headNodes = new SkipListNodeBlock[this.headerBlock.MaxLevelOfSkipList];
            long currentSkipListNodeBlockPos = indexBlock.SkipListHeadNodePos;
            for (int i = headNodes.Length - 1; i >= 0; i--)
            {
                if (currentSkipListNodeBlockPos == 0)
                { throw new Exception(string.Format("max level [{0}] != real max level [{1}]", headNodes.Length, headNodes.Length - 1 - i)); }
                SkipListNodeBlock skipListNodeBlock = fileStream.ReadBlock<SkipListNodeBlock>(currentSkipListNodeBlockPos);
                headNodes[i] = skipListNodeBlock;
                if (i != headNodes.Length - 1)
                { headNodes[i + 1].DownObj = headNodes[i]; }
                currentSkipListNodeBlockPos = skipListNodeBlock.DownPos;
            }
            return headNodes;
        }

        /// <summary>
        /// 创建初始状态的数据库文件。
        /// </summary>
        /// <param name="fullname">数据库文件据对路径。</param>
        /// <param name="config">数据库配置信息。</param>
        private void CreateDB(string fullname, DBConfig config)
        {
            FileInfo fileInfo = new FileInfo(fullname);
            Directory.CreateDirectory(fileInfo.DirectoryName);
            using (FileStream fs = new FileStream(fullname, FileMode.CreateNew, FileAccess.Write, FileShare.None, Consts.pageSize))
            {
                PageHeaderBlock page = new PageHeaderBlock() { OccupiedBytes = Consts.pageSize, AvailableBytes = 0, };
                fs.WriteBlock(page);

                DBHeaderBlock dbHeader = new DBHeaderBlock() { MaxLevelOfSkipList = config.MaxLevelOfSkipList, ProbabilityOfSkipList = config.ProbabilityOfSkipList , MaxSunkCountInMemory = config.MaxSunkCountInMemory, LockTimeout = config.LockTimeout, ThisPos = fs.Position };
                fs.WriteBlock(dbHeader);

                TableBlock tableHead = new TableBlock() { ThisPos = fs.Position, };
                fs.WriteBlock(tableHead);

                byte[] leftSpace = new byte[Consts.pageSize - fs.Length];
                fs.Write(leftSpace, 0, leftSpace.Length);

                BlockCache.TryRemoveFloatingBlock(page);
                BlockCache.TryRemoveFloatingBlock(dbHeader);
                BlockCache.TryRemoveFloatingBlock(tableHead);
                BlockCache.TryRemoveSunkBlock(page);
                BlockCache.TryRemoveSunkBlock(dbHeader);
                BlockCache.TryRemoveSunkBlock(tableHead);
            }
        }

    }
}
