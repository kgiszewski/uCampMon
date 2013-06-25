using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.Net;
using System.Text;
using System.IO;
using System.Xml;

using umbraco.BusinessLogic;

namespace uCampMon
{
    /// <summary>
    /// Summary description for CampaignMonitor
    /// </summary>
    [WebService(Namespace = "http://franklinfueling.com/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]

    [System.Web.Script.Services.ScriptService]
    public class CampaignMonitor : System.Web.Services.WebService
    {

        private JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
        private Dictionary<string, string> returnValue = new Dictionary<string, string>();
        private Dictionary<string, string> cmRequest = new Dictionary<string, string>();
        private enum status { SUCCESS, CM_ERROR, EMAIL_INVALID, NO_LIST };
        private int cmResponseCode = 200;
        private string api_key;
        private string listID = "";
        private Subscriber subscriber = null;

        public CampaignMonitor()
        {
            api_key = System.Web.Configuration.WebConfigurationManager.AppSettings["uCampMon:apiKey"];
        }

        [WebMethod]
        public Dictionary<string, string> RegisterSubscriber(string email, string listIndex, string customFieldName, string customFieldValue, string resubscribe, string resubsribeAutoResponders)
        {
            cmRequest.Add("EmailAddress", email);
            cmRequest.Add("Name", "");
            listID = System.Web.Configuration.WebConfigurationManager.AppSettings["uCampMon:listKey" + listIndex];

            List<CustomField> customFields = new List<CustomField>() { };
            subscriber = getSubscriber("http://api.createsend.com/api/v3/subscribers/" + listID + ".json?email=" + email, email);

            //add in custom choices already made
            if (subscriber != null)
            {
                foreach (CustomField cf in subscriber.CustomFields)
                {
                    if (cf.Key == customFieldName)
                    {
                        customFields.Add(new CustomField() { Key = customFieldName, Value = cf.Value });
                    }
                }
            }

            customFields.Add(new CustomField() { Key = customFieldName, Value = customFieldValue });
            cmRequest.Add("CustomFields", jsonSerializer.Serialize(customFields));

            cmRequest.Add("Resubscribe", resubscribe);
            cmRequest.Add("RestartSubscriptionBasedAutoresponders", resubsribeAutoResponders);
            sendSubscribeRequest("http://api.createsend.com/api/v3/subscribers/" + listID + ".json", email);
            return returnValue;
        }              

        #region helper methods
        private Subscriber getSubscriber(string uri, string email)
        {
            if (checkEmail(email))
            {
                WebRequest request = createWebRequest(uri, "GET");
                string responseFromServer = "";

                try
                {
                    // Get the response.
                    WebResponse response = request.GetResponse();

                    // Get the stream containing content returned by the server.
                    Stream dataStream = response.GetResponseStream();

                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);

                    // Read the content.
                    responseFromServer = reader.ReadToEnd();

                    reader.Close();
                    dataStream.Close();
                    response.Close();

                    Subscriber subscriber = jsonSerializer.Deserialize<Subscriber>(responseFromServer);

                    //returnValue.Add("GetSub", responseFromServer);
                    return subscriber;
                }
                catch (WebException we)
                {
                    if (we.Status == WebExceptionStatus.ProtocolError)
                    {
                        cmResponseCode = (int)((HttpWebResponse)we.Response).StatusCode;
                    }
                    else
                    {
                        cmResponseCode = 500;
                    }

                    returnValue.Add("currentStatus", cmResponseCode.ToString());
                }
            }
            return null;
        }

        private bool checkEmail(string email)
        {
            Regex regex = new Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$");
            Match match = regex.Match(email);
            return match.Success;
        }

        private WebRequest createWebRequest(string uri, string method)
        {
            // Create a request using a URL that can receive a post. 
            WebRequest request = WebRequest.Create(uri);

            //auth
            String authstring = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(api_key + ":blank"));
            request.Headers.Add("Authorization", "Basic " + authstring);

            // Set the Method property of the request to POST.
            request.Method = method;

            if (method == "POST")
            {
                // Create POST data and convert it to a byte array.
                //string postData = "This is a test that posts this string to a Web server.";
                string postData = jsonSerializer.Serialize(cmRequest).Replace("\\", "").Replace("\"[", "[").Replace("]\"", "]");

                Log.Add(LogTypes.Debug, 0, "cmsRequest=>"+postData);

                byte[] byteArray = Encoding.UTF8.GetBytes(postData);

                // Set the ContentType property of the WebRequest.
                request.ContentType = "application/json";

                // Set the ContentLength property of the WebRequest.
                request.ContentLength = byteArray.Length;

                // Get the request stream.
                Stream dataStream = request.GetRequestStream();

                // Write the data to the request stream.
                dataStream.Write(byteArray, 0, byteArray.Length);

                // Close the Stream object.
                dataStream.Close();
            }

            return request;
        }

        private void sendSubscribeRequest(string uri, string email)
        {
            if (checkEmail(email))
            {
                WebRequest request = createWebRequest(uri, "POST");

                string responseFromServer = "";
                status statusCode = status.SUCCESS;
                string message = umbraco.library.GetDictionaryItem("ThankYou");//TODO: localize

                try
                {
                    // Get the response.
                    WebResponse response = request.GetResponse();

                    // Get the stream containing content returned by the server.
                    Stream dataStream = response.GetResponseStream();

                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);

                    // Read the content.
                    responseFromServer = reader.ReadToEnd();

                    reader.Close();
                    dataStream.Close();
                    response.Close();
                }
                catch (WebException we)
                {
                    statusCode = status.CM_ERROR;
                    message = "There was an error with your registration, please try back later.";//TODO: Localize

                    if (we.Status == WebExceptionStatus.ProtocolError)
                    {
                        cmResponseCode = (int)((HttpWebResponse)we.Response).StatusCode;
                    }
                    else
                    {
                        cmResponseCode = 500;
                    }
                }

                returnValue.Add("status", statusCode.ToString());
                returnValue.Add("cmResponse", responseFromServer);
                returnValue.Add("message", message);
                returnValue.Add("code", cmResponseCode.ToString());
                //returnValue.Add("jsonSent", postData);

            }
            else
            {
                returnValue.Add("status", status.EMAIL_INVALID.ToString());
                returnValue.Add("message", umbraco.library.GetDictionaryItem("RequiredEmail"));//TODO: Localize
            }
        }
        #endregion helper methods

    }

    #region helper classes
    public class CustomField
    {
        public string Key = "";
        public string Value = "";
    }

    public class Subscriber
    {
        public string EmailAddress = "";
        public List<CustomField> CustomFields = new List<CustomField>() { };
    }
    #endregion helper classes
}
