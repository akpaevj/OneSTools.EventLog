using System;
using System.IO;
using System.Reflection;

namespace OneSTools.EventLog
{
    internal static class StreamReaderExtensions
    {
        private static readonly FieldInfo charPosField = typeof(StreamReader).GetField("_charPos",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        private static readonly FieldInfo byteLenField = typeof(StreamReader).GetField("_byteLen",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        private static readonly FieldInfo charBufferField = typeof(StreamReader).GetField("_charBuffer",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        public static long GetPosition(this StreamReader reader)
        {
            // shift position back from BaseStream.Position by the number of bytes read
            // into internal buffer.
            var byteLen = (int) byteLenField.GetValue(reader);
            var position = reader.BaseStream.Position - byteLen;

            // if we have consumed chars from the buffer we need to calculate how many
            // bytes they represent in the current encoding and add that to the position.
            var charPos = (int) charPosField.GetValue(reader);
            if (charPos > 0)
            {
                var charBuffer = (char[]) charBufferField.GetValue(reader);
                var encoding = reader.CurrentEncoding;
                var bytesConsumed = encoding.GetBytes(charBuffer, 0, charPos).Length;
                position += bytesConsumed;
            }

            return position;
        }

        public static void SetPosition(this StreamReader reader, long position)
        {
            reader.DiscardBufferedData();
            reader.BaseStream.Seek(position, SeekOrigin.Begin);

            if (reader.BaseStream.Position != position)
                throw new Exception("Couldn't set the stream position");
        }
    }
}