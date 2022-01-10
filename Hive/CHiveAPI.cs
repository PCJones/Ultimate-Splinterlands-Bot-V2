using System;
using System.Collections;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HiveAPI.CS
{
	public class CHiveAPI
	{
		#region Constants
		const string STR_RESULT = "result";
		const string STR_ERROR = "error";
		const string STR_FORMAT = "Unexpected result format";
		#endregion

		#region Variables
		private HttpClient m_oHttpClient;
		private string m_strURL;
		#endregion

		#region Constructors
		public CHiveAPI(HttpClient oHttpClient, string strURL)
		{
			m_oHttpClient = oHttpClient;
			m_strURL = strURL;
		}
		#endregion

		#region Private methods
		private async Task<string> Post(string strMethod, ArrayList arrParams = null)
		{
			string		strResult = string.Empty;
			Hashtable arrRequest = new();

			arrRequest.Add("id", 1);
			arrRequest.Add("jsonrpc", "2.0");
			arrRequest.Add("method", strMethod);
			if(arrParams != null)
			{
				if( arrParams.Count == 1 && arrParams[0].GetType() == typeof(Hashtable) ) {
					arrRequest.Add("params", arrParams[0]);
				}
				else
				{
					arrRequest.Add("params", arrParams);
				}
			}
			string strJson = JsonConvert.SerializeObject(arrRequest);
			using (var oResponse = await m_oHttpClient.PostAsync(m_strURL, new StringContent(strJson, System.Text.Encoding.UTF8, "application/json")))
			{
				oResponse.EnsureSuccessStatusCode();
				strResult = await oResponse.Content.ReadAsStringAsync();
			}
			return strResult;
		}
		private string SendRequest(string strMethod, ArrayList aParams = null)
		{
			using(Task<string> t = Post(strMethod, aParams)) {
				t.Wait();
				return t.Result;
			}
		}
		private static object ProcessResult(JObject obj)
		{
			if (obj[STR_RESULT] != null)
			{
				return obj[STR_RESULT];
			}
			if (obj[STR_ERROR] != null)
			{
				throw new Exception(obj[STR_ERROR].ToString());
			}
			throw new Exception(STR_FORMAT);
		}
        #endregion

        #region protected methods
        protected void call_api_sub(string strMethod)
        {
            SendRequest(strMethod);
        }
        protected void call_api_sub(string strMethod, ArrayList arrParams)
        {
            SendRequest(strMethod, arrParams);
        }
        protected JObject call_api(string strMethod)
        {
			return (JObject)ProcessResult(JsonConvert.DeserializeObject<JObject>(SendRequest(strMethod)));
        }
        protected JObject call_api(string strMethod, ArrayList arrParams)
        {
            return (JObject)ProcessResult(JsonConvert.DeserializeObject<JObject>(SendRequest(strMethod, arrParams)));
        }
		protected JArray call_api_array(string strMethod)
		{
			return (JArray)ProcessResult(JsonConvert.DeserializeObject<JObject>(SendRequest(strMethod)));
		}
		protected JArray call_api_array(string strMethod, ArrayList arrParams)
        {
			return (JArray)ProcessResult(JsonConvert.DeserializeObject<JObject>(SendRequest(strMethod, arrParams)));
        }
        protected JValue call_api_value(string strMethod)
        {
            return (JValue)ProcessResult(JsonConvert.DeserializeObject<JObject> (SendRequest(strMethod)));
        }
        protected JValue call_api_value(string strMethod, ArrayList arrParams)
        {
            return (JValue)ProcessResult(JsonConvert.DeserializeObject<JObject>(SendRequest(strMethod, arrParams)));
        }
        protected JToken call_api_token(string strMethod, ArrayList arrParams)
        {
            return (JToken)ProcessResult(JsonConvert.DeserializeObject<JObject>(SendRequest(strMethod, arrParams)));
        }
        protected JToken call_api_token(string strMethod)
        {
            return (JToken)ProcessResult(JsonConvert.DeserializeObject<JObject> (SendRequest(strMethod)));
        }
        #endregion
    }
}
