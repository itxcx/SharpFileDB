﻿using SharpFileDB;
using System;
using System.Drawing;
using System.Runtime.Serialization;

namespace SharpFileDB.TestConsole
{
    /// <summary>
    /// demo file object
    /// </summary>
    [Serializable]
    public class Cat : FileObject, ISerializable
    {
        /// <summary>
        /// Used for serialization.
        /// <para>序列化需要此构造函数。</para>
        /// </summary>
        public Cat() { }

        public string Name { get; set; }
        public int Legs { get; set; }

        public Image HeadPortrait { get; set; }

        public override string ToString()
        {
            return string.Format("{0}, Name: {1}, Legs: {2}", base.ToString(), Name, Legs);
        }

        const string strName = "Name";
        const string strLegs = "Legs";
        const string strHeadPortraitString = "headPortraitString";

        #region ISerializable 成员

        /// <summary>
        /// This method will be invoked automatically when IFormatter.Serialize() is called.
        /// <para>当使用IFormatter.Serialize()时会自动调用此方法。</para>
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(strName, this.Name);

            info.AddValue(strLegs, this.Legs);

            if (this.HeadPortrait != null)
            {
                byte[] bytes = ImageHelper.ImageToBytes(this.HeadPortrait);
                string str = Convert.ToBase64String(bytes);
                info.AddValue(strHeadPortraitString, str);
            }
            else
            {
                info.AddValue(strHeadPortraitString, string.Empty);
            }
        }

        #endregion

        /// <summary>
        /// This constructor will be invoked automatically when IFormatter.Deserialize() is called.
        /// <para>当使用IFormatter.Deserialize()时会自动调用此构造函数。</para>
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected Cat(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            this.Name = info.GetValue(strName, typeof(string)) as string;

            this.Legs = (int)info.GetValue(strLegs, typeof(int));

            object obj = info.GetValue(strHeadPortraitString, typeof(string));
            if (obj != null)
            {
                string str = obj as string;
                if (str != string.Empty)
                {
                    byte[] bytes = Convert.FromBase64String(str);
                    Image image = ImageHelper.BytesToImage(bytes);
                    this.HeadPortrait = image;
                }
            }
        }
    }
}
