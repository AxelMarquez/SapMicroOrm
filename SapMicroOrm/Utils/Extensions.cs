using FastMember;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SapMicroOrm.Utils
{
    public static class SapExtensions
    {
        public static string SerializeProperties<T>(IEnumerable<string> properties, T entity, TypeAccessor accessor = null)
        {
            var result = new StringBuilder();
            accessor = accessor ?? TypeAccessor.Create(typeof(T));

            foreach (var prop in properties)
            {
                result.Append($"{prop}='{accessor[entity, prop]}', ");
            }

            result.ReplaceLastOccurrence(",", string.Empty);

            return result.ToString();
        }

        public static bool IsEmpty<T>(this IEnumerable<T> enumerable)
        {
            if (typeof(T) == typeof(char))
            {
                return string.IsNullOrWhiteSpace(enumerable as string);
            }

            if (enumerable == null) return true;
            return enumerable.Count() == 0;
        }

        public static bool IsNumber(this Type type)
        {
            return
                type == typeof(decimal)
                || type == typeof(double)
                || type == typeof(int)
                || type == typeof(Int64)
                || type == typeof(short);
        }

        public static string Join<T>(this IEnumerable<T> rows, string separator = ",")
        {
            return string.Join(separator, rows);
        }

        public static string ReplaceLastOccurrence(this string Source, string Find, string Replace)
        {
            //Source: https://stackoverflow.com/questions/14825949/replace-the-last-occurrence-of-a-word-in-a-string-c-sharp
            int place = Source.LastIndexOf(Find);

            if (place == -1)
                return Source;

            string result = Source.Remove(place, Find.Length).Insert(place, Replace);
            return result;
        }

        public static StringBuilder ReplaceLastOccurrence(this StringBuilder Source, string Find, string Replace)
        {
            //Source: https://stackoverflow.com/questions/14825949/replace-the-last-occurrence-of-a-word-in-a-string-c-sharp
            int place = Source.LastIndexOf(Find);

            if (place == -1)
                return Source;

            var result = Source.Remove(place, Find.Length).Insert(place, Replace);
            return result;
        }

        public static int LastIndexOf(this StringBuilder sb, string value)
        {
            value = value.Reverse();
            int index;
            int length = value.Length;
            int startIndex = sb.Length - 1;

            for (int i = startIndex; i >= 0; i--)
            {
                if (sb[i] == value[0])
                {
                    index = 0;
                    while ((index < length) && (sb[i - index] == value[index]))
                        ++index;

                    if (index == length)
                        return i - (length - 1);
                }
            }

            return -1;
        }

        public static string Reverse(this string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
    }
}
