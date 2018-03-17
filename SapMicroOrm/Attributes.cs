using System;

namespace SapMicroOrm
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class Alias : Attribute
    {
        public string _Alias { get; private set; }

        public Alias(string alias)
        {
            _Alias = alias;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class Key : Attribute
    {
    }
}
