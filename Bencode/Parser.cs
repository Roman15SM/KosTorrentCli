using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KosTorrentCli.Bencode
{
    public class Parser
    {
        public TorrentDataTrie Parse(string path)
        {
            if (!File.Exists(path))
                throw new Exception("Invalid Path");

            string readText = File.ReadAllText(path, Encoding.ASCII);
            var readBytes = File.ReadAllBytes(path);

            return Parse(readText, readBytes);
        }

        public TorrentDataTrie Parse(string readText, byte[] readBytes)
        {
            var structureStack = new Stack<TorrentMetaItem>();
            var data = new TorrentDataTrie();

            for (var i = 0; i < readText.Length; ++i)
            {
                switch (readText[i])
                {
                    case 'd':
                        AddChildCollection(TorrentMetaType.Dictionary, structureStack, data, i);
                        break;
                    case 'i':
                        var intValue = IntGetter(readText, ref i);

                        structureStack.Peek().Children.Add(new TorrentMetaItem
                        {
                            Type = TorrentMetaType.Integer,
                            Value = intValue.ToString()
                        });
                        break;
                    case 'l':
                        AddChildCollection(TorrentMetaType.List, structureStack, data, i);
                        break;
                    case 'e':
                        var collection = structureStack.Pop();

                        if (collection.Type == TorrentMetaType.Dictionary)
                        {
                            for (var iter = 0; iter < collection.Children.Count; ++iter)
                            {
                                data.AddItem(collection.Children[iter].Value, collection.Children[iter + 1]);
                                ++iter;
                            }
                        }

                        var subArray = new byte[i - collection.StartPosition + 1];
                        Array.Copy(readBytes, collection.StartPosition, subArray, 0, subArray.Length);

                        collection.BencodeByteData = subArray.ToList();
                        break;
                    default:
                        if (char.IsDigit(readText[i]))
                        {
                            var value = StringGetter(readText, ref i);
                            //compensate cycle increment
                            --i;

                            var subStrArray = new byte[value.Length];
                            Array.Copy(readBytes, i - subStrArray.Length, subStrArray, 0, subStrArray.Length);

                            structureStack.Peek().Children.Add(new TorrentMetaItem
                            {
                                Type = TorrentMetaType.String,
                                Value = value,
                                BencodeByteData = subStrArray.ToList() 
                            });
                        }
                        else
                            throw new Exception($"Unknown Bencode identifier: {readText[i]}");
                        break;
                }
            }

            return data;
        }

        private void AddChildCollection(TorrentMetaType childType, Stack<TorrentMetaItem> structure, TorrentDataTrie data, int position)
        {
            var newChild = new TorrentMetaItem
            {
                Type = childType,
                StartPosition = position
            };

            if (structure.Count > 0)
            {
                var currentParent = structure.Peek();
                currentParent.Children.Add(newChild);
            }
            else
                data.Children.Add(newChild);

            structure.Push(newChild);
        }

        private int IntGetter(string source, ref int position)
        {
            var sNumber = "";
            ++position;

            while (source[position] != 'e')
            {
                sNumber += source[position];
                ++position;
            }

            //e will be skipped by next iteration in for cycle.
            _ = int.TryParse(sNumber, out var num);
            return num;
        }

        private string StringGetter(string source, ref int position)
        {
            var sLength = "";

            while (char.IsDigit(source[position]))
            {
                sLength += source[position];
                ++position;
            }

            //skip semicolon
            ++position;
            var startIndex = position;
            _ = int.TryParse(sLength, out var length);

            position += length;
            return source.Substring(startIndex, length);
        }
    }
}
