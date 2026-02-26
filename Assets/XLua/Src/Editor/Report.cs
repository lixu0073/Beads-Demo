#if UNITY_5_6_OR_NEWER

namespace XLua
{
    using UnityEngine;
    using UnityEditor;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    [InitializeOnLoad]
    public class Report
    {
        private const string PREFS_KEY = "XLuaReport";
        private const string DIALOG_MSG_FORMAT = @"���Ƿǳ�ע��������˽Ȩ����Ҫ�ռ����±�Ҫ��Ϣ���ṩ���õķ���

XLua�汾��{0}
����汾��{1}
�豸��ʶ��{2}

We attach great importance to your privacy and need to collect the following necessary information to provide better services:

XLua Version: {0}
Unity Version: {1}
Device Identifier: {2}";

        static Report()
        {
            if (EditorPrefs.HasKey(PREFS_KEY) && !EditorPrefs.GetBool(PREFS_KEY))
                return;

            var version = "2.1.16";
            var engine = Application.unityVersion;
            var machine = SystemInfo.deviceUniqueIdentifier;
            var msg = string.Format("cmd=0&tag=glcoud.xlua.report&version={0}&engine={1}&machine_name={2}", version, engine, machine);

            if (!EditorPrefs.HasKey(PREFS_KEY))
            {
                var dialogMsg = string.Format(DIALOG_MSG_FORMAT, version, engine, machine);
                var result = EditorUtility.DisplayDialog(string.Empty, dialogMsg, "���� Allow", "�ܾ� Deny");
                EditorPrefs.SetBool(PREFS_KEY, result);
                if (!result)
                    return;
            }

            new Thread(() =>
            {
                var data = Encoding.UTF8.GetBytes(msg);
                var client = new UdpClient();
                client.Send(data, data.Length, "101.226.141.148", 8080);
                client.Close();
            }).Start();
        }
    }
}

#endif
