﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace compilembed
{
    public class CompileMessage
    {
        public string action { get; set; }
        public string severity { get; set; }
        public string type { get; set; }
        public double percent { get; set; }
        public string file { get; set; }
        public string line { get; set; }
        public string text { get; set; }
        public string message { get; set; }
    }

    public class CompileResultData
    {
        public bool task_complete { get; set; }
        public string task_status { get; set; }
        public string task_id { get; set; }
        public CompileMessage[] new_messages { get; set; }
        public bool compilation_success { get; set; }
    }

    public class CompileResult
    {
        public CompileResultData data { get; set; }
    }

    public class CompileStartResponse
    {

        public int code { get; set; }
        public string[] errors { get; set; }
        public CompileResult result { get; set; }
    }
    public class MBEDOnlineCompile
    {
        private string authInfo = string.Empty;
        private string taskId = string.Empty;

        public MBEDOnlineCompile(string userName, string password)
        {
            authInfo = userName + ":" + password;
            authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
        }

        public void StartCompile(string platform, string program = null, string repo = null, bool clean = false)
        {
            WebRequest request = WebRequest.Create("https://developer.mbed.org/api/v2/tasks/compiler/start/");
            request.Headers["Authorization"] = "Basic " + authInfo;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            string requestPayload = string.Format("platform={0}&clean={1}", platform, clean ? "True" : "False");
            if (!string.IsNullOrEmpty(program))
            {
                requestPayload += string.Format("&program={0}", program);
            }
            if (!string.IsNullOrEmpty(repo))
            {
                requestPayload += string.Format("&repo={0}", repo);
            }
            byte[] encodedPayload = Encoding.UTF8.GetBytes(requestPayload);
            request.GetRequestStream().Write(encodedPayload, 0, Encoding.UTF8.GetBytes(requestPayload).Length);
            WebResponse response = request.GetResponse();
            CompileStartResponse compileResponse = new JavaScriptSerializer().Deserialize<CompileStartResponse>(new StreamReader(response.GetResponseStream()).ReadToEnd());
            taskId = compileResponse.result.data.task_id;
        }

        public bool PollStatus(ICollection<string> messages, out bool failed)
        {
            string requestURL = string.Format("https://developer.mbed.org/api/v2/tasks/compiler/output/{0}", HttpUtility.UrlEncode(taskId));
            WebRequest request = WebRequest.Create(requestURL);
            request.Headers["Authorization"] = "Basic " + authInfo;
            request.Method = "GET";
            WebResponse response = request.GetResponse();
            string jsonResponse = new StreamReader(response.GetResponseStream()).ReadToEnd();
            CompileStartResponse compileResponse = new JavaScriptSerializer().Deserialize<CompileStartResponse>(jsonResponse);
            foreach (CompileMessage msg in compileResponse.result.data.new_messages)
            {
                if (msg.type == "cc")
                {
                    messages.Add(string.Format("{0}: {1}: {2}: {3}", msg.severity, msg.file, msg.line, msg.message));
                    messages.Add(string.Format("{0}", msg.text));
                }
                else if (msg.type == "progress")
                {
                    messages.Add(string.Format("{0}: {1} ({2:F} %)", msg.action, msg.file, msg.percent));
                }
            }
            failed = !compileResponse.result.data.compilation_success;
            return compileResponse.result.data.task_complete;
        }
    }
}
