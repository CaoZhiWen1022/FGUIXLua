// 此文件由配置工具自动生成，请勿手动修改
//
// Unity 依赖：请在工程 Packages/manifest.json 的 dependencies 中添加
//   "com.unity.nuget.newtonsoft-json": "3.0.2"

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameConfig
{
    /// <summary>
    /// 配置表名称枚举与表名映射
    /// </summary>
    public enum GameConfigName
    {
        _列表,
        _常数表,
    }

    internal static class GameConfigNameMap
    {
        public static readonly System.Collections.Generic.Dictionary<GameConfigName, string> TableKeys =
            new System.Collections.Generic.Dictionary<GameConfigName, string>
            {
            { GameConfigName._列表, "ListCfg" },
            { GameConfigName._常数表, "ConstCfg" },
            };

        public static string GetTableKey(GameConfigName cfgName)
        {
            string tableKey;
            return TableKeys.TryGetValue(cfgName, out tableKey) ? tableKey : cfgName.ToString();
        }
    }

    /// <summary>
    /// 游戏数据管理器
    /// 单例模式，需外部加载 TextAsset 后调用 ParseBin
    /// 可多次调用 ParseBin 合并多个 bin（默认 gamedata.bin，也可为表自定义 bin）
    /// 依赖：在 Packages/manifest.json 添加 "com.unity.nuget.newtonsoft-json": "3.0.2"
    /// </summary>
    public class GameConfigMgr
    {
        private static GameConfigMgr _ins;
        private JObject _data;

        private GameConfigMgr()
        {
        }

        public static GameConfigMgr Ins
        {
            get
            {
                if (_ins == null)
                {
                    _ins = new GameConfigMgr();
                }
                return _ins;
            }
        }

        /// <summary>
        /// 解析并合并一份 Unity TextAsset（gzip bin），可多次传入
        /// </summary>
        public void ParseBin(TextAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("GameConfigMgr: parseBin data is null or undefined");
                return;
            }
            MergeParsed(DecodeBuffer(asset.bytes));
        }

        /// <summary>
        /// 解析并合并一份原始 gzip 字节，可多次传入
        /// </summary>
        public void ParseBin(byte[] bytes)
        {
            if (bytes == null)
            {
                Debug.LogError("GameConfigMgr: parseBin data is null or undefined");
                return;
            }
            MergeParsed(DecodeBuffer(bytes));
        }

        private JObject DecodeBuffer(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
            {
                return JObject.Parse(reader.ReadToEnd());
            }
        }

        private void MergeParsed(JObject parsed)
        {
            if (parsed == null)
            {
                return;
            }
            if (_data == null)
            {
                _data = new JObject();
            }
            foreach (var property in parsed.Properties())
            {
                _data[property.Name] = property.Value;
            }
        }

        /// <summary>
        /// 获取指定表的数据
        /// </summary>
        public T GetConfig<T>(GameConfigName cfgName)
        {
            var token = GetRawConfig(GameConfigNameMap.GetTableKey(cfgName));
            if (token == null)
            {
                return default(T);
            }
            return token.ToObject<T>();
        }

        /// <summary>
        /// 按导出表名获取原始 JSON 节点，供 xLua 封装使用
        /// </summary>
        public JToken GetRawConfig(string tableName)
        {
            if (_data == null)
            {
                throw new Exception("Game data not initialized. Call ParseBin() first.");
            }
            if (string.IsNullOrEmpty(tableName))
            {
                return null;
            }

            var token = _data[tableName];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            return token;
        }

        /// <summary>
        /// 按导出表名和主键 id 获取原始 JSON 节点
        /// </summary>
        public JToken GetRawConfigById(string tableName, string id)
        {
            var token = GetRawConfig(tableName) as JArray;
            if (token == null)
            {
                Debug.LogError("配置表" + tableName + "不存在id:" + id);
                return null;
            }

            for (var i = 0; i < token.Count; i++)
            {
                var item = token[i];
                if (item != null && (string)item["id"] == id)
                {
                    return item;
                }
            }

            Debug.LogError("配置表" + tableName + "不存在id:" + id);
            return null;
        }

        /// <summary>
        /// 按主键 id 获取列表表中的一项
        /// </summary>
        public T GetConfigById<T>(GameConfigName cfgName, string id)
        {
            if (_data == null)
            {
                throw new Exception("Game data not initialized. Call ParseBin() first.");
            }

            var token = _data[GameConfigNameMap.GetTableKey(cfgName)] as JArray;
            if (token == null)
            {
                Debug.LogError("配置表" + cfgName + "不存在id:" + id);
                return default(T);
            }

            for (var i = 0; i < token.Count; i++)
            {
                var item = token[i];
                if (item != null && (string)item["id"] == id)
                {
                    return item.ToObject<T>();
                }
            }

            Debug.LogError("配置表" + cfgName + "不存在id:" + id);
            return default(T);
        }

        /// <summary>
        /// 根据模板字段等值筛选列表表
        /// </summary>
        public List<T> GetConfigByTemplate<T>(GameConfigName cfgName, JObject template)
        {
            if (_data == null)
            {
                throw new Exception("Game data not initialized. Call ParseBin() first.");
            }

            var token = _data[GameConfigNameMap.GetTableKey(cfgName)] as JArray;
            if (token == null)
            {
                Debug.LogError("配置表" + cfgName + "不存在");
                return new List<T>();
            }

            if (template == null)
            {
                return token.ToObject<List<T>>() ?? new List<T>();
            }

            var result = new List<T>();
            for (var i = 0; i < token.Count; i++)
            {
                var item = token[i] as JObject;
                if (item == null)
                {
                    continue;
                }

                var matched = true;
                foreach (var property in template.Properties())
                {
                    if (property.Value == null || property.Value.Type == JTokenType.Null || property.Value.Type == JTokenType.Undefined)
                    {
                        continue;
                    }
                    var itemValue = item[property.Name];
                    if (itemValue == null || !JToken.DeepEquals(itemValue, property.Value))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    result.Add(item.ToObject<T>());
                }
            }
            return result;
        }
    }
}
