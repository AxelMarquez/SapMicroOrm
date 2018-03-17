﻿using System;
using System.Reflection;

namespace SapMicroOrm.Utils
{
    static class FastMemberExtensions
    {
        //Source: https://stackoverflow.com/questions/21976125/how-to-get-the-attribute-data-of-a-member-with-fastmember

        public static T GetPrivateField<T>(this object obj, string name)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = obj.GetType();
            FieldInfo field = type.GetField(name, flags);
            return (T)field.GetValue(obj);
        }

        public static T GetPrivateProperty<T>(this object obj, string name)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = obj.GetType();
            PropertyInfo field = type.GetProperty(name, flags);
            return (T)field.GetValue(obj, null);
        }

        public static MemberInfo GetMemberInfo(this FastMember.Member member)
        {
            return GetPrivateField<MemberInfo>(member, "member");
        }

        public static T GetMemberAttribute<T>(this FastMember.Member member) where T : Attribute
        {
            return GetPrivateField<MemberInfo>(member, "member").GetCustomAttribute<T>();
        }
    }
}
