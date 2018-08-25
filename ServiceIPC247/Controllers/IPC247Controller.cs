using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Hosting;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ServiceIPC247.Controllers
{
    public class IPC247Controller : ApiController
    {
		#region Function
		string str_connect = System.Configuration.ConfigurationManager.ConnectionStrings["IPC247ConnectionString"].ConnectionString;
		
		private DataTable ExecuteNonQuery(SqlCommand cmd)
		{
			using (SqlDataAdapter dt = new SqlDataAdapter(cmd))
			{
				dt.SelectCommand = cmd;
				cmd.CommandTimeout = 100000;
				DataTable dtTemp = new DataTable();

				dt.Fill(dtTemp);
				return dtTemp;
			}
		}
		private List<Dictionary<string, object>> ConvertDataTableToListObject(DataTable dt)
		{
			List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
			Dictionary<string, object> row;
			foreach (DataRow dr in dt.Rows)
			{
				row = new Dictionary<string, object>();
				foreach (DataColumn col in dt.Columns)
				{
					row.Add(col.ColumnName, dr[col]);
				}
				rows.Add(row);
			}
			return rows;
		}
		public static void WriteLog(string file, string message, EventLogEntryType type = EventLogEntryType.Error)
		{
			// Write logs to file
			try
			{
				string LOG_FILE_PATH = HostingEnvironment.MapPath("~/App_Data/LOG");
				string folder = LOG_FILE_PATH + "/" + DateTime.Today.ToString("yyyyMMdd");
				if (!Directory.Exists(folder))
				{
					Directory.CreateDirectory(folder);
				}
				if (!Directory.Exists(folder+ "/" + file))
				{
					Directory.CreateDirectory(folder + "/" + file);
				}

				var filePath = folder + "/" + file + "/" + "ERROR_logs.txt";


				if (!File.Exists(filePath))
				{
					// Create a file to write to.
					using (StreamWriter sw = File.CreateText(filePath))
					{
						sw.WriteLine("---------------------------------------");
						sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
						sw.WriteLine(message);
						sw.WriteLine("---------------------------------------");
					}
				}
				else
				{
					// This text is always added, making the file longer over time
					// if it is not deleted.
					using (StreamWriter sw = File.AppendText(filePath))
					{
						sw.WriteLine("---------------------------------------");
						sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
						sw.WriteLine(message);
						sw.WriteLine("---------------------------------------");
					}
				}
			}
			catch {
			}
		}

		#endregion Function

		#region extension
		[HttpPost]
		public HttpResponseMessage sp_extension_Login(JObject json)
		{
			DataTable dt = new DataTable();
			try
			{
				var UserName = json.GetValue("UserName") != null ? (json.GetValue("UserName").Value<String>().Trim() ?? "") : "";
				var Password = json.GetValue("Password") != null ? (json.GetValue("Password").Value<String>().Trim() ?? "") : "";
				
				using (SqlConnection conn = new SqlConnection(str_connect))
				{
					using (SqlCommand cmd = new SqlCommand("sp_extension_Login", conn))
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = UserName;
						cmd.Parameters.Add("@Password", SqlDbType.NVarChar).Value = Password;
						conn.Open();
						dt = ExecuteNonQuery(cmd);
					}
				}
				return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Data = ConvertDataTableToListObject(dt) });
			}
			catch (Exception ex)
			{
				WriteLog("sp_extension_Login", "ERROR : " + ex.ToString());
			}
			return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Data = ConvertDataTableToListObject(dt) });
		}
		[HttpGet]
		public HttpResponseMessage sp_extension_DeleteAllProduct()
		{
			DataTable dt = new DataTable();
			try
			{
				using (SqlConnection conn = new SqlConnection(str_connect))
				{
					using (SqlCommand cmd = new SqlCommand("sp_extension_DeleteProduct", conn))
					{
						cmd.CommandType = CommandType.StoredProcedure;
						conn.Open();
						dt = ExecuteNonQuery(cmd);
					}
				}
			}
			catch (Exception ex)
			{
				WriteLog("sp_extension_DeleteAllProduct", "ERROR : " + ex.ToString());
			}
			return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Data = ConvertDataTableToListObject(dt) });
		}

		[HttpPost]
		public HttpResponseMessage sp_extension_ImportProduct(JObject json)
		{
			DataTable dt = new DataTable();
			try
			{
				var UserID = json.GetValue("UserName") != null ? (json.GetValue("UserName").Value<String>().Trim() ?? "") : "";
				var jsondata = json.GetValue("Data");
				using (SqlConnection conn = new SqlConnection(str_connect))
				{
					conn.Open();

					// List<Product> ProductList = JsonConvert.DeserializeObject<List<Product>>(jsondata.ToString());
					using (SqlCommand sqlcm = new SqlCommand(String.Format("Delete [T_Product_temp] where CreateBy='{0}'", UserID), conn))
					{
						sqlcm.CommandType = CommandType.Text;
						sqlcm.ExecuteNonQuery();
					}

					SqlBulkCopy bulkInsert = new SqlBulkCopy(conn);
					bulkInsert.DestinationTableName = "T_Product_temp";
					dt = (DataTable)JsonConvert.DeserializeObject(jsondata.ToString(), (typeof(DataTable)));
					dt.Columns.Remove("Id");
					bulkInsert.WriteToServer(dt);

				}
				using (SqlConnection conn = new SqlConnection(str_connect))
				{
					using (SqlCommand cmd = new SqlCommand("sp_extension_ImportProduct", conn))
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = UserID;
						conn.Open();
						dt = ExecuteNonQuery(cmd);
					}
				}
			}
			catch (Exception ex)
			{
				WriteLog("sp_extension_ImportProduct", "ERROR : " + ex.ToString());
				return Request.CreateResponse(HttpStatusCode.OK, new { Success = false, Data = ex.ToString() });
			}
			return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Data = "Đã Import Dữ Liệu Thành Công" });
		}

		[HttpGet]
		public HttpResponseMessage sp_extension_GetDataByStore(string sql_Exec)
		{
			DataTable dt = new DataTable();
			try
			{
				using (SqlConnection conn = new SqlConnection(str_connect))
				{
					using (SqlCommand cmd = new SqlCommand(sql_Exec, conn))
					{
						cmd.CommandType = CommandType.StoredProcedure;
						conn.Open();
						dt = ExecuteNonQuery(cmd);
					}
				}
			}
			catch (Exception ex)
			{
				WriteLog("sp_extension_GetDataByStore", "ERROR : "+ sql_Exec+" - " + ex.ToString());
				return Request.CreateResponse(HttpStatusCode.OK, new { Success = false, Data = ConvertDataTableToListObject(dt) });
			}
			return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Data = ConvertDataTableToListObject(dt) });
		}
		[HttpGet]
		public HttpResponseMessage sp_extension_GetDataByQueryString(string str_Query)
		{
			DataTable dt = new DataTable();
			try
			{
				using (SqlConnection conn = new SqlConnection(str_connect))
				{
					using (SqlCommand cmd = new SqlCommand(str_Query, conn))
					{
						cmd.CommandType = CommandType.Text;
						conn.Open();
						dt = ExecuteNonQuery(cmd);
					}
				}
			}
			catch (Exception ex)
			{
				WriteLog("sp_extension_GetDataByStore", "ERROR : " + str_Query + " - " + ex.ToString());
				return Request.CreateResponse(HttpStatusCode.OK, new { Success = false, Data = ConvertDataTableToListObject(dt) });
			}
			return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Data = ConvertDataTableToListObject(dt) });
		}
		[HttpPost]
		public HttpResponseMessage sp_extension_SaveQuote(JObject json)
		{
			DataTable dt = new DataTable();
			try
			{
				var StoreProcedure = json.GetValue("StoreProcedure") != null ? (json.GetValue("StoreProcedure").Value<String>().Trim() ?? "") : "";
				var Parameter = json.GetValue("Param");

				JArray a = JArray.Parse(Parameter.ToString());

				using (SqlConnection conn = new SqlConnection(str_connect))
				{
					using (SqlCommand cmd = new SqlCommand(StoreProcedure, conn))
					{
						cmd.CommandType = CommandType.StoredProcedure;
						conn.Open();
						foreach (JObject o in a.Children<JObject>())
						{
							ObjectParame ob = o.ToObject<ObjectParame>();
							if (ob.Type == "Base64")
							{
								ob.Value = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(ob.Value));
							}
							ob.Key = string.Format("@{0}", ob.Key);
							cmd.Parameters.Add(ob.Key, SqlDbType.NVarChar).Value = ob.Value;
						}
						dt = ExecuteNonQuery(cmd);
					}
				}
			}
			catch (Exception ex)
			{
				WriteLog("sp_extension_GetDataByStore", "ERROR : "  + " - " + ex.ToString());
				return Request.CreateResponse(HttpStatusCode.OK, new { Success = false, Data = ConvertDataTableToListObject(dt) });
			}
			return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Data = ConvertDataTableToListObject(dt) });
		}

		private class ObjectParame
		{
			public string Key { get; set; }
			public string Value { get; set; }

			public string Type { get; set; }
		}
		#endregion extension
	
	}
}
