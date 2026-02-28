using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Aor.Persistence
{
    /*
     * 用户接口
     */
    [Serializable]
    public class Vector3Serializable
    {
        // transform
        public float x, y, z;
        public Vector3Serializable() {

        }
        public Vector3Serializable(Vector3 v) {
            value = v;
        }
        public static Vector3Serializable[] FromVector3Array(Vector3[] array) {
            var res = new Vector3Serializable[array.Length];
            for (int i = 0; i < array.Length; i++) {
                res[i] = new Vector3Serializable(array[i]);
            }
            return res;
        }
        public static Vector3[] ToVector3Array(Vector3Serializable[] array) {
            var res = new Vector3[array.Length];
            for (int i = 0; i < array.Length; i++) {
                res[i] = array[i].value;
            }
            return res;
        }
        public override string ToString() {
            return $"Vector3({x},{y},{z})";
        }
        [JsonIgnore]
        public Vector3 value {
            get {
                return new Vector3(x, y, z);
            }
            set {
                x = value.x;
                y = value.y;
                z = value.z;
            }
        }
    }

    [Serializable]
    public class QuaternionSerializable
    {
        float x, y, z, w;
        public QuaternionSerializable() {

        }
        public QuaternionSerializable(Quaternion v) {
            value = v;
        }
        [JsonIgnore]
        public Quaternion value {
            get {
                return new Quaternion(x, y, z, w);
            }
            set {
                x = value.x;
                y = value.y;
                z = value.z;
                w = value.w;
            }
        }
    }
    [Serializable]
    public class TransformSerializable
    {
        // transform
        public Vector3Serializable position = new Vector3Serializable();
        public Vector3Serializable scale = new Vector3Serializable();
        public QuaternionSerializable quaternion = new QuaternionSerializable();
        public void SaveFrom(Transform transform) {
            position.value = transform.position;
            scale.value = transform.localScale;
            quaternion.value = transform.localRotation;
        }
        public void LoadTo(Transform game_object) {
            game_object.transform.position = position.value;
            game_object.transform.localScale = scale.value;
            game_object.transform.localRotation = quaternion.value;
        }
    }
}
