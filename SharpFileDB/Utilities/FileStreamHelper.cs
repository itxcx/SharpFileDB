﻿using SharpFileDB.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace SharpFileDB.Utilities
{
    /// <summary>
    /// <see cref="FileStream"/>类型的辅助类。
    /// </summary>
    public static class FileStreamHelper
    {

        /// <summary>
        /// 把此块写入文件。
        /// </summary>
        /// <param name="fileStream">数据库文件流。</param>
        /// <param name="block"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBlock(this FileStream fileStream, Block block)
        {
            if (fileStream.Length < block.ThisPos)
            {
                fileStream.SetLength(block.ThisPos);
            }
            fileStream.Seek(block.ThisPos, SeekOrigin.Begin);
            Consts.formatter.Serialize(fileStream, block);
        }

        /// <summary>
        /// 从文件流中给定的位置读取一个块。
        /// </summary>
        /// <typeparam name="T">块的类型。</typeparam>
        /// <param name="fileStream"></param>
        /// <param name="position">块所在位置。</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadBlock<T>(this FileStream fileStream, long position) where T : Block
        {
            fileStream.Seek(position, SeekOrigin.Begin);
            object obj = Consts.formatter.Deserialize(fileStream);
            T result = obj as T;
            result.ThisPos = position;
            return result;
        }

        /// <summary>
        /// 从文件流中给定的位置读取一个由块为结点组成的文件链表。
        /// </summary>
        /// <typeparam name="T">块的类型。</typeparam>
        /// <param name="fileStream"></param>
        /// <param name="position">第一个块的位置。</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] ReadBlocks<T>(this FileStream fileStream, long position) where T : Block, ILinkedNode<T>
        {
            List<T> list = new List<T>();
            T item = ReadBlock<T>(fileStream, position);
            list.Add(item);

            while (item.NextPos != 0)
            {
                item.NextObj = ReadBlock<T>(fileStream, item.NextPos);
                list.Add(item.NextObj);
                item = item.NextObj;
            }

            return list.ToArray();
        }


        ///// <summary>
        ///// 从文件流中给定的位置读取一个对象。
        ///// </summary>
        ///// <typeparam name="T">对象的类型。</typeparam>
        ///// <param name="fileStream"></param>
        ///// <param name="position">对象所在位置。</param>
        ///// <returns></returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static T ReadObject<T>(this FileStream fileStream, long position)
        //{
        //    if (position < 0)
        //    { return default(T); }
        //    fileStream.Seek(position, SeekOrigin.Begin);
        //    object obj = Consts.formatter.Deserialize(fileStream);
        //    T result = (T)obj;
        //    return result;
        //}
    }
}
