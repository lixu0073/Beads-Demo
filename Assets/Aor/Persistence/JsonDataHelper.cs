using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Aor.Persistence
{
    class WritablePropertiesOnlyResolver :DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
            // IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
            // return props.Where(p => p.PropertyName.StartsWith("_") == false).Where(p => p.Writable).
            //    Where(p=>p.PropertyType.GetCustomAttributes(typeof(System.NonSerializedAttribute), false).Length == 0).ToList();
            {
                var props = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    // .Where(p => !p.Name.StartsWith("_"))
                    .Where(p => p.FieldType.GetCustomAttributes(typeof(System.NonSerializedAttribute), false).Length == 0)
                    .Select(p => base.CreateProperty(p, memberSerialization))
                    .ToList();
                props.ForEach(p => { p.Writable = true; p.Readable = true; });
                return props;
            }
        }
    }
    public class DummyStorageType { }
    public class AorSerializationBinder :DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName) {
            try {
                // 尝试默认绑定逻辑
                return base.BindToType(assemblyName, typeName);
            } catch {
                // 处理类型缺失的情况
                Debug.Log($"[Newtonsoft.Json] type `{typeName}` cannot be found in assembly `{assemblyName}`. but this error will be ignored.");
                // 返回一个 null，让 Newtonsoft.Json 继续处理
                return typeof(DummyStorageType);
            }
        }
    }
    public class JsonDataHelper
    {
        static JsonDataHelper _instance;
#if YY_DEBUG
        static bool debug = false;  // 发布的时候，debug千万不要打开。否则，因为要记录debug log，会很慢很慢。
#else
        static bool debug = false;
#endif
        public static JsonDataHelper instance {
            get {
                if (_instance == null) {
                    _instance = new JsonDataHelper();
                }
                return _instance;
            }
        }

        JsonSerializer serializer;
        List<string> errors = new List<string>();
        bool serializing;
        string current_path;
        Type current_type;
        ITraceWriter trace_writer = new MemoryTraceWriter();

        public JsonSerializer Serializer => this.serializer;
        private JsonDataHelper() {
            this.serializer = JsonSerializer.Create(this.GetSettings());
        }
        JsonSerializerSettings GetSettings() {
            var settings = new JsonSerializerSettings();
            settings.FloatFormatHandling = FloatFormatHandling.DefaultValue;
            settings.FloatParseHandling = FloatParseHandling.Double;
            settings.MissingMemberHandling = MissingMemberHandling.Ignore;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Error;
            settings.NullValueHandling = NullValueHandling.Ignore;
            settings.TypeNameHandling = TypeNameHandling.Auto;
            settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

            // 如果Ignore了，会导致：1.外部修改者无法得到全部字段信息 2.如果初始值不是默认值，会导致为默认值的字段，反序列化后的值是错误的。
            // 例如类的属性 a，默认值是true，现在是 a=false，序列化时被忽略了，导致反序列化时也被忽略，导致使用了默认值true，和序列化之前的值不同。
            settings.DefaultValueHandling = DefaultValueHandling.Include;

            settings.ObjectCreationHandling = ObjectCreationHandling.Reuse;
            settings.MetadataPropertyHandling = MetadataPropertyHandling.Ignore;

            // 如果一个类没有无参构造函数，就会自动调用有参构造函数，调用时所有参数都是默认值，可能会导致有问题。
            settings.ConstructorHandling = ConstructorHandling.Default;

            settings.Error = ErrorHandler;
            if (debug) {
                settings.TraceWriter = trace_writer;
                settings.Formatting = Formatting.Indented;
            }
            settings.ContractResolver = new WritablePropertiesOnlyResolver();
            settings.MetadataPropertyHandling = MetadataPropertyHandling.Default;
            if (EditorUtil.IsBuild) {
                // 忽略掉类型缺失的报错。
                settings.SerializationBinder = new AorSerializationBinder();
            }
            return settings;
        }
        private void ErrorHandler(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e) {
            // 记录异常
            var err_msg = e.ErrorContext.Error.Message;
            errors.Add(err_msg);
            Debug.LogError("JsonData error, is_serilizaing:" + serializing + ", message:" + err_msg + ", path:" + this.current_path +
                ", type:" + this.current_type);
            Debug.LogError("error path:" + e.ErrorContext.Path);
            var str = trace_writer.ToString();
            Debug.LogError("trace_end:\n" + str.Substring(str.Length - 1000));
            Debug.LogError("trace_start:\n" + str.Substring(0, 1000));

            e.ErrorContext.Handled = true;
        }
        public string GetAllErrors() {
            if (this.errors.Count == 0) return null;
            return string.Join("\n--> ", this.errors.ToArray());
        }
        public void SerializeToFile(string filename, object data) {
            serializing = true;
            current_path = filename;
            current_type = data.GetType();

            using (var writer = File.CreateText(filename)) {
                this.serializer.Serialize(writer, data, data.GetType());
            }
        }
        public TObject CopyByJson<TObject>(TObject obj) {
            // 处理null值
            if (obj == null)
                return default(TObject);

            // 使用StringWriter进行序列化
            using (StringWriter sw = new StringWriter())
            using (JsonTextWriter writer = new JsonTextWriter(sw)) {
                // 序列化对象到JSON
                serializer.Serialize(writer, obj);
                string jsonString = sw.ToString();

                // 使用StringReader进行反序列化
                using (StringReader sr = new StringReader(jsonString))
                using (JsonTextReader reader = new JsonTextReader(sr)) {
                    // 从JSON反序列化回对象
                    return (TObject)serializer.Deserialize(reader, typeof(TObject));
                }
            }
        }
        public object DeserializeFromFile(string filename, Type data_type) {
            if (!File.Exists(filename)) {
                // 这里故意不报错的，依靠这个返回null来判断存档是否存在。
                Debug.Log("JSON file doesn't exists:" + filename);
                return null;
            }
            current_path = filename;
            serializing = false;
            current_type = data_type;

            using (var reader = File.OpenText(filename)) {
                return this.serializer.Deserialize(reader, data_type);
            }
        }
    }
}
