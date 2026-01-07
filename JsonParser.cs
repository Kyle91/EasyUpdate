using System;
using System.Collections.Generic;
using System.Text;

namespace EasyUpdate
{
    /// <summary>
    /// 简易JSON解析器，兼容.NET Framework 4.0，无需外部依赖
    /// </summary>
    public static class JsonParser
    {
        public static Models.UpdatePayload ParseUpdatePayload(string json)
        {
            var payload = new Models.UpdatePayload();

            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                throw new Exception("Invalid JSON format");

            var content = json.Substring(1, json.Length - 2);

            payload.UpdateContent = GetStringValue(content, "update_content");
            payload.MainProcess = GetStringValue(content, "main_process");
            payload.MainExe = GetStringValue(content, "main_exe");

            var listJson = GetArrayValue(content, "list");
            if (!string.IsNullOrEmpty(listJson))
            {
                payload.List = ParseUpdateList(listJson);
            }

            return payload;
        }

        private static List<Models.UpdateListItem> ParseUpdateList(string arrayJson)
        {
            var list = new List<Models.UpdateListItem>();

            arrayJson = arrayJson.Trim();
            if (!arrayJson.StartsWith("[") || !arrayJson.EndsWith("]"))
                return list;

            var content = arrayJson.Substring(1, arrayJson.Length - 2).Trim();
            if (string.IsNullOrEmpty(content))
                return list;

            var objects = SplitJsonObjects(content);
            foreach (var objJson in objects)
            {
                var item = ParseUpdateListItem(objJson);
                if (item != null)
                    list.Add(item);
            }

            return list;
        }

        private static Models.UpdateListItem ParseUpdateListItem(string json)
        {
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return null;

            var content = json.Substring(1, json.Length - 2);

            var item = new Models.UpdateListItem
            {
                Name = GetStringValue(content, "name"),
                Url = GetStringValue(content, "url"),
                Md5 = GetStringValue(content, "md5"),
                IsZip = GetBoolValue(content, "is_zip"),
                ZipPass = GetStringValue(content, "zip_pass"),
                ExtractName = GetStringValue(content, "extract_name"),
                SavePath = GetStringValue(content, "save_path")
            };

            return item;
        }

        private static string GetStringValue(string json, string key)
        {
            var searchKey = "\"" + key + "\"";
            var keyIndex = json.IndexOf(searchKey);
            if (keyIndex == -1)
                return null;

            var colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex == -1)
                return null;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length)
                return null;

            if (json[valueStart] == 'n' && json.Substring(valueStart).StartsWith("null"))
                return null;

            if (json[valueStart] != '"')
                return null;

            var sb = new StringBuilder();
            var i = valueStart + 1;
            while (i < json.Length)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    var nextChar = json[i + 1];
                    switch (nextChar)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 5 < json.Length)
                            {
                                var hex = json.Substring(i + 2, 4);
                                var code = Convert.ToInt32(hex, 16);
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(nextChar); break;
                    }
                    i += 2;
                }
                else if (json[i] == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(json[i]);
                    i++;
                }
            }

            return sb.ToString();
        }

        private static bool GetBoolValue(string json, string key)
        {
            var searchKey = "\"" + key + "\"";
            var keyIndex = json.IndexOf(searchKey);
            if (keyIndex == -1)
                return false;

            var colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex == -1)
                return false;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length)
                return false;

            var remaining = json.Substring(valueStart).TrimStart();
            return remaining.StartsWith("true");
        }

        private static string GetArrayValue(string json, string key)
        {
            var searchKey = "\"" + key + "\"";
            var keyIndex = json.IndexOf(searchKey);
            if (keyIndex == -1)
                return null;

            var colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex == -1)
                return null;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length || json[valueStart] != '[')
                return null;

            var depth = 0;
            var i = valueStart;
            while (i < json.Length)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') depth--;

                if (depth == 0)
                    return json.Substring(valueStart, i - valueStart + 1);

                i++;
            }

            return null;
        }

        private static List<string> SplitJsonObjects(string content)
        {
            var objects = new List<string>();
            var depth = 0;
            var start = -1;
            var inString = false;
            var escape = false;

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];

                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{')
                {
                    if (depth == 0)
                        start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        objects.Add(content.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return objects;
        }
    }
}
