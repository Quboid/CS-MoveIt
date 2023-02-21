using System;
using System.Xml.Serialization;
using UnityEngine;

namespace MoveIt
{
    public class Selection
    {
        public Version GetVersion()
        {
            Version v;
            try
            {
                if (version.Contains(" ")) v = new Version(version.Substring(0, version.IndexOf(' ')));
                else v = new Version(version);
            }
            catch
            {
                v = new Version(QCommonLib.QVersion.Version());
            }
            return v;
        }

        public Vector3 center;
        public string version;
        public bool includesPO;
        public float terrainHeight;

        [XmlElement("state")]
        public InstanceState[] states;
    }
}
