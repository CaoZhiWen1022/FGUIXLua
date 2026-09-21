// 此文件由配置工具自动生成，请勿手动修改
//
// 供 xLua 调用的非泛型接口。Lua 无法直接使用 GameConfigMgr 的泛型方法。
// 请在 XLua 的 LuaCallCSharp 列表中加入：
//   GameConfig.XLuaGameConfigMgr
//   GameConfig.GameConfigName
//
// Lua 只通过本类取表：GetConfigByTableName / ParseBinBytes
// 返回 Dictionary / List，不要把 JToken 交给 Lua。

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using XLua;

namespace GameConfig
{
    [LuaCallCSharp]
    public static class XLuaGameConfigMgr
    {
        /// <summary>
        /// 解析并合并一份 Unity TextAsset（gzip bin），可多次传入
        /// </summary>
        public static void ParseBin(TextAsset asset)
        {
            GameConfigMgr.Ins.ParseBin(asset);
        }

        /// <summary>
        /// 解析并合并一份原始 gzip 字节，可多次传入
        /// </summary>
        public static void ParseBinBytes(byte[] bytes)
        {
            GameConfigMgr.Ins.ParseBin(bytes);
        }

        /// <summary>
        /// 获取整张表。列表表返回数组，常数表返回对象。字段用 row["id"] 访问。
        /// </summary>
        public static object GetConfig(GameConfigName cfgName)
        {
            return ToLuaValue(GameConfigMgr.Ins.GetConfig<JToken>(cfgName));
        }

        /// <summary>
        /// 按导出表名获取整张表
        /// </summary>
        public static object GetConfigByTableName(string tableName)
        {
            return ToLuaValue(GameConfigMgr.Ins.GetRawConfig(tableName));
        }

        /// <summary>
        /// 按主键 id 获取列表表中的一项
        /// </summary>
        public static object GetConfigById(GameConfigName cfgName, string id)
        {
            return ToLuaValue(GameConfigMgr.Ins.GetConfigById<JToken>(cfgName, id));
        }

        /// <summary>
        /// 按导出表名和主键 id 获取列表表中的一项
        /// </summary>
        public static object GetConfigByTableNameAndId(string tableName, string id)
        {
            return ToLuaValue(GameConfigMgr.Ins.GetRawConfigById(tableName, id));
        }

        private static object ToLuaValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            if (token.Type == JTokenType.Object)
            {
                var dict = new Dictionary<string, object>();
                foreach (var property in ((JObject)token).Properties())
                {
                    dict[property.Name] = ToLuaValue(property.Value);
                }
                return dict;
            }

            if (token.Type == JTokenType.Array)
            {
                var list = new List<object>();
                foreach (var item in (JArray)token)
                {
                    list.Add(ToLuaValue(item));
                }
                return list;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<long>();
            }
            if (token.Type == JTokenType.Float)
            {
                return token.Value<double>();
            }
            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
            if (token.Type == JTokenType.String)
            {
                return token.Value<string>();
            }
            return token.ToString();
        }
    }
}
