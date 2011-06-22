using System;
using System.Text;
using System.Net;
using System.Xml;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Collections.Specialized;

using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web.Script.Serialization;

namespace MailgunClient
{
    /// <summary>
    /// Mailgun.Init() lets you initialize the library. 
    /// You need API Key. You may provide API URL if for some reason it differs from standard.
    /// </summary>
    public class Mailgun
    {
        /// <summary>
        ///  Initialize the library with standard MailGun API URL
        /// </summary>
        /// <param name="apiKey"></param>
        public static void Init (string apiKey)
        {
            Init (apiKey, "https://mailgun.net/api");
        }

        public static void Init (string apiKey, string apiUrl)
        {
            _apiUrl = apiUrl;
            if (!_apiUrl.EndsWith ("/"))
                _apiUrl += "/";
            _cc = new CredentialCache ();
            Uri url = new Uri (_apiUrl);
            _cc.Remove (url, "Basic");
            _cc.Add (url, "Basic", new NetworkCredential ("api_key", apiKey));
        }

        static internal string ApiUrl {
            get {
                if (string.IsNullOrEmpty (_apiUrl))
                    throw new MailgunException ("Call Mailgun.Init() first");
                return _apiUrl;
            }
        }

        static internal HttpWebRequest OpenRequest (string url, string method)
        {
            // Expect: 100-continue fails behind transparent squid proxy
            ServicePointManager.Expect100Continue = false;
            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create (url);
            wr.Method = method;
            // Turn off proxy auto-detection, it causes long delay on first request
            wr.Proxy = null;
            wr.Credentials = _cc;
            return wr;
        }

        static internal HttpWebResponse SendRequest (HttpWebRequest request)
        {
            try {
                return (HttpWebResponse)request.GetResponse ();
            } catch (WebException ex) {
                throw MailgunException.Wrap (ex);
            }
        }

        static internal Stream GetRequestStream (HttpWebRequest request)
        {
            try {
                return request.GetRequestStream ();
            } catch (WebException ex) {
                throw MailgunException.Wrap (ex);
            }
        }

        private static CredentialCache _cc;
        private static string _apiUrl;
    }

    [Serializable]
    [ComVisible(true)]
    public class MailgunException : ApplicationException
    {
        public MailgunException (string message) : base(message)
        {
        }
        public MailgunException (string message, Exception inner) : base(message, inner)
        {
        }

        public static MailgunException Wrap (WebException ex)
        {
            // Mailgun provides detailed error message in HTTP status line.
            //
            // WebException.ToString() overrides status description for standard
            // HTTP codes such as 404 (Not found), 409 (Conflict), 500 (Internal error).
            //
            // Fortunately, in .NET we can see what is wrong as follows:
            HttpWebResponse response = ex.Response as HttpWebResponse;
            if (response != null) {
                string message = string.Format ("{0} {1}", (int)response.StatusCode, response.StatusDescription);
                return new MailgunException (message, ex);
            } else
                return new MailgunException (ex.Message, ex);
        }
    }

    internal struct ResourceInfo
    {
        public ResourceInfo (string collectionName, string elementName)
        {
            this.collectionName = collectionName;
            this.elementName = elementName;
        }
        public string collectionName;
        public string elementName;
    }

    /// <summary>
    /// Base class providing basic ActiveResource functionality
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class MailgunResource<T> where T : MailgunResource<T>, new()
    {
        /// <summary>
        /// Resource ID.
        /// A resource load the ID in Create/Save/Find operations.
        /// </summary>
        public string Id {
            // our target is C# 2.0, don't use automatic properties
            get { return _id; }
            set { _id = value; }
        }
        private string _id;

        /// <summary>
        /// Create new resource.
        /// Throw if "same resorce" already exists. The meaning of "same" depends. 
        /// For example, "same Route exists" if there are route with same pattern and destination.
        /// </summary>
        public void Create ()
        {
            update (collectionUrl, "POST");
        }

        /// <summary>
        /// Save modifications or create new resource.
        /// </summary>
        public void Save ()
        {
            if (string.IsNullOrEmpty (Id))
                Create ();
            else
                update (resourceUrl, "PUT");
        }

        /// <summary>
        /// Delete the resource. 
        /// No error if the resource with given ID does not exist (already deleted).
        /// </summary>
        public void Delete ()
        {
            using (Mailgun.SendRequest (Mailgun.OpenRequest (resourceUrl, "DELETE"))) {
            }
        }

        /// <summary>
        /// Find all resources.
        /// </summary>
        /// <returns></returns>
        public static List<T> Find ()
        {
            string collectionUrl = (new T ()).collectionUrl;
            // dirty but simple
            HttpWebRequest wr = Mailgun.OpenRequest (collectionUrl, "GET");
            using (HttpWebResponse r = Mailgun.SendRequest (wr)) {
                if (isResponseXml (r)) {
                    using (XmlReader xr = XmlReader.Create (r.GetResponseStream ()))
                        return readList (xr);
                }
            }
            return new List<T> ();
        }

        protected void update (string url, string method)
        {
            update (url, method, true);
        }

        protected void update (string url, string method, bool readUpdatedResource)
        {
            HttpWebRequest wr = Mailgun.OpenRequest (url, method);
            wr.ContentType = "text/xml";
            using (Stream rs = Mailgun.GetRequestStream (wr))
                writeXml (rs);

            using (HttpWebResponse r = Mailgun.SendRequest (wr)) {
                if (readUpdatedResource && isResponseXml (r)) {
                    using (XmlReader xr = XmlReader.Create (r.GetResponseStream ()))
                        readThis (xr);
                }
            }
        }

        protected static bool isResponseXml (HttpWebResponse r)
        {
            return r.ContentLength > 0 && r.ContentType.StartsWith ("text/xml");
        }

        /// <summary>
        /// Read the resource.
        /// Precondition: XmlReader position is on resource elementName tag.
        /// Postcondition: XmlReader position is after elementName closing tag.
        /// </summary>
        /// <param name="xr"></param>
        protected virtual void readThis (XmlReader xr)
        {
            xr.ReadStartElement (elementName);
            while (xr.IsStartElement ()) {
                string propName = xr.LocalName;
                object propVal = null;
                if (!xr.IsEmptyElement) {
                    xr.ReadStartElement (xr.LocalName);
                    if (string.IsNullOrEmpty (xr.GetAttribute ("nil")))
                        propVal = xr.ReadContentAs (mapARType (xr.GetAttribute ("type")), null);
                    xr.ReadEndElement ();
                } else
                    xr.Read ();
                onReadProperty (propName, propVal);
            }
            xr.ReadEndElement ();
        }

        /// <summary>
        /// Read list of resources
        /// Precondition: XmlReader position before/on collection tag - any tag having type="array" attribute.
        /// Postcondition:XmlReader position after collection closing tag.
        /// </summary>
        /// <param name="xr"></param>
        /// <returns></returns>
        protected static List<T> readList (XmlReader xr)
        {
            // find <collectionName type="array">
            while (!xr.IsStartElement () || xr.GetAttribute ("type") != "array")
                xr.Read ();
            
            List<T> res = new List<T> ();
            // for each nested element, readThis()
            if (!xr.IsEmptyElement) {
                xr.ReadStartElement (xr.LocalName);
                while (xr.IsStartElement ()) {
                    T resource = new T ();
                    resource.readThis (xr);
                    res.Add (resource);
                }
                xr.ReadEndElement ();
            } else
                xr.Read ();
            return res;
        }

        /// <summary>
        /// Write resource as XML, including xml declaration. Encoding is UTF-8.
        /// </summary>
        /// <param name="output"></param>
        protected virtual void writeXml (Stream output)
        {
            XmlWriterSettings xs = new XmlWriterSettings ();
            xs.Encoding = Encoding.UTF8;
            using (XmlWriter xw = XmlWriter.Create (output, xs)) {
                xw.WriteStartDocument ();
                xw.WriteStartElement (elementName);
                writeInnerXml (xw);
                xw.WriteEndElement ();
            }
        }

        /// <summary>
        /// When overriden, write resource inner content 
        /// (anything between resource opening and closing tags)
        /// </summary>
        /// <param name="xw"></param>
        protected virtual void writeInnerXml (XmlWriter xw)
        {
            // new resource must not write ID
            if (!string.IsNullOrEmpty (Id))
                xw.WriteElementString ("id", Id);
        }

        /// <summary>
        /// Write property value.
        /// Currently supports only integers and strings (and null value).
        //
        /// Companion function is mapARType().
        /// </summary>
        protected void writeARProperty (XmlWriter xw, string propName, object propVal)
        {
            //Empty   0
            //Object  1
            //DBNull  2
            //Boolean 3
            //Char    4
            //SByte   5
            //Byte    6
            //Int16   7
            //UInt16  8
            //Int32   9
            //UInt32  10
            //Int64   11
            //UInt64  12
            //Single  13
            //Double  14
            //Decimal 15
            //DateTime 16
            //String  18
            xw.WriteStartElement (propName);
            if (propVal != null) {
                int typecode = (int)Type.GetTypeCode (propVal.GetType ());
                if (5 <= typecode && typecode <= 12)
                    xw.WriteAttributeString ("type", "integer");
                // next line supports int/string only
                xw.WriteString (propVal.ToString ());
            } else
                xw.WriteAttributeString ("nil", "true");
            xw.WriteEndElement ();
        }

        /// <summary>
        /// Map ActiveResource type to clr-type.
        /// Currently supports integers and strings.
        /// 
        /// Companion function is writeARProperty().
        /// </summary>
        /// <param name="activeResourceType"></param>
        /// <returns></returns>
        protected static Type mapARType (string activeResourceType)
        {
            switch (activeResourceType) {
            case "integer":
                return typeof(int);
            default:
                return typeof(string);
            }
        }


        /// <summary>
        /// Override and assign property value.
        /// </summary>
        /// <param name="propName">Name of the property as seen in XML</param>
        /// <param name="propVal">Property value, casted to approproate CLR Type</param>
        /// <returns></returns>
        protected virtual bool onReadProperty (string propName, object propVal)
        {
            if (propName == "id") {
                Id = (string)propVal;
                return true;
            }
            return false;
        }

        internal abstract ResourceInfo getResourceInfo ();

        protected string resourceUrl {
            get { return Mailgun.ApiUrl + collectionName + "/" + Id + ".xml"; }
        }

        protected string collectionUrl {
            get { return Mailgun.ApiUrl + collectionName + ".xml"; }
        }

        protected string collectionName {
            get { return getResourceInfo ().collectionName; }
        }

        protected string elementName {
            get { return getResourceInfo ().elementName; }
        }
    }

    /// <summary>
    /// Provide Upsert() custom resource method
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class MailgunResourceUpsertable<T> : MailgunResource<T> where T : MailgunResourceUpsertable<T>, new()
    {
        /// <summary>
        /// There are 2 differences between Upsert() and Save(). 
        /// - Upsert does not throw exception if object already exist. 
        /// - Upsert does not load Id property of the object. 
        ///
        /// It ensures that resource exists on the server and does not syncronize client object instance.
        /// In order to modify "upserted" object, you need to Find() it first.
        /// Upsert() is designed to simplify resource creation, when you want to skip existing resources.
        /// </summary>
        public void Upsert ()
        {
            update (Mailgun.ApiUrl + collectionName + "/upsert.xml", "POST", false);
        }
    }

    /// <summary>
    /// Route represents the basic rule: the message for particular recipient R is forwarded to destination/callback D.
    /// Route has 2 properties: pattern and destination. The pair (pattern, destination) must be unique.
    /// </summary>
    public class Route : MailgunResourceUpsertable<Route>
    {
        public string Pattern {
            get { return _pattern; }
            set { _pattern = value; }
        }
        public string Destination {
            get { return _destination; }
            set { _destination = value; }
        }
        private string _pattern, _destination;

        public Route ()
        {
        }

        /// <summary>
        /// Construct the route.
        /// </summary>
        /// <param name="pattern">
        /// The pattern for matching the recipient.
        /// There are 4 types of patterns:
        /// 1. '*' - match all.
        /// 2. exact string match (foo@bar.com)
        /// 3. a domain pattern, i.e. a string like "*@example.com" - matches all emails going to example.com
        /// 4. a regular expression
        /// </param>
        /// <param name="destination">
        /// 1 An email address.
        /// 2 HTTP/HTTPS URL. A message will be HTTP POSTed there.
        /// </param>
        public Route (string pattern, string destination)
        {
            Pattern = pattern;
            Destination = destination;
        }

        protected override bool onReadProperty (string propName, object propVal)
        {
            if (base.onReadProperty (propName, propVal))
                return true;
            switch (propName) {
            case "pattern":
                Pattern = (string)propVal;
                return true;
            case "destination":
                Destination = (string)propVal;
                return true;
            default:
                return false;
            }
        }

        protected override void writeInnerXml (XmlWriter xw)
        {
            base.writeInnerXml (xw);
            writeARProperty (xw, "pattern", Pattern);
            writeARProperty (xw, "destination", Destination);
        }

        public override string ToString ()
        {
            return string.Format ("Route({0}, {1}, {2})", Pattern, Destination, Id);
        }

        internal override ResourceInfo getResourceInfo ()
        {
            return _resInfo;
        }

        // our target is C# 2.0, don't use "structure initialization syntax"
        private static ResourceInfo _resInfo = new ResourceInfo ("routes", "route");
    }

    /// <summary>
    /// Mailbox captures all mail arriving to it's address.
    /// Email will be stored on the server and can be later accessed via IMAP or POP3                                                                                   
    /// protocols.                                                                                                                                                
    ///                                                                                                                                                           
    /// Mailbox has several properties:                                                                                                                           
    ///                                                                                                                                                           
    /// alex@gmail.com                                                                                                                                            
    ///  ^      ^                                                                                                                                                 
    ///  |      |                                                                                                                                                 
    /// user    domain                                                                                                                                            
    ///                                                                                                                                                           
    /// and a password                                                                                                                                            
    ///                                                                                                                                                           
    /// user and domain can not be changed for an existing mailbox.   
    /// </summary>
    public class Mailbox : MailgunResourceUpsertable<Mailbox>
    {
        public string User {
            get { return _user; }
            set { _user = value; }
        }
        public string Domain {
            get { return _domain; }
            set { _domain = value; }
        }
        public string Password {
            get { return _password; }
            set { _password = value; }
        }

        public string Size {
            set { _size = value; }
            get { return _size; }
        }

        private string _user, _domain, _password, _size;

        public Mailbox ()
        {
        }

        /// <summary>
        /// Construct the Mailbox.
        /// </summary>
        /// <param name="user">
        /// </param>
        /// <param name="domain">
        /// </param>
        /// <param name="password">
        /// </param>
        public Mailbox (string user, string domain, string password)
        {
            User = user;
            Domain = domain;
            Password = password;
        }

        protected override bool onReadProperty (string propName, object propVal)
        {
            if (base.onReadProperty (propName, propVal))
                return true;
            switch (propName) {
            case "user":
                User = (string)propVal;
                return true;
            case "domain":
                Domain = (string)propVal;
                return true;
            case "size":
                Size = (string)propVal;
                return true;
            default:
                return false;
            }
        }

        protected override void writeInnerXml (XmlWriter xw)
        {
            base.writeInnerXml (xw);
            writeARProperty (xw, "user", User);
            writeARProperty (xw, "domain", Domain);
            writeARProperty (xw, "password", Password);
        }

        public override string ToString ()
        {
            return string.Format ("Mailbox({0}@{1} {2})", User, Domain, Size);
        }

        internal override ResourceInfo getResourceInfo ()
        {
            return _resInfo;
        }

        // our target is C# 2.0, don't use "structure initialization syntax"
        private static ResourceInfo _resInfo = new ResourceInfo ("mailboxes", "mailbox");

        public static void UpsertFromCsv (byte[] mailboxes)
        {
            HttpWebRequest wr = Mailgun.OpenRequest (Mailgun.ApiUrl + "mailboxes.txt", "POST");
            wr.ContentLength = mailboxes.Length;
            wr.ContentType = "text/plain";
            using (Stream rs = Mailgun.GetRequestStream (wr)) {
                rs.Write (mailboxes, 0, mailboxes.Length);
            }
            using (Mailgun.SendRequest (wr)) {
            }
        }
    }

    public class MailgunMessage
    {
        public static string MAILGUN_TAG = "X-Mailgun-Tag";

        public class Options
        {
            private Dictionary<string, Dictionary<string, string>> options;
            
            public Options()
            {
                options = new Dictionary<string, Dictionary<string, string>>();
                options["headers"] = new Dictionary<string, string>();
            }

            public void SetHeader(string header, string value)
            {
                options["headers"][header] = value;
            }

            public String toJSON()
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                return serializer.Serialize(options);
            }
        }

        public static void SendText (string sender, string recipients, string subject, string text)
        {
            SendText (sender, recipients, subject, text, "");
        }


        public static void SendText (string sender, string recipients, string subject, string text, string servername)
        {
            SendText (sender, recipients, subject, text, servername, null);
        }

        public static void SendText (string sender, string recipients, string subject, string text, MailgunMessage.Options options)
        {
            SendText (sender, recipients, subject, text, "", options);
        }

        /// <summary>
        /// Send plain-text message 
        /// </summary>
        /// <param name="sender">sender specification</param>
        /// <param name="recipients">comma- or semicolon-separated list of recipients specifications</param>
        /// <param name="subject">message subject</param>
        /// <param name="text">message text</param>
        /// <param name="servername">sending server (can be empty, use 'best' server)</param>
        /// <param name="options">sending options (e.g. add headers to message)</param>
        public static void SendText (string sender, string recipients, string subject, string text, string servername, MailgunMessage.Options options)
        {
            NameValueCollection req = new NameValueCollection ();
            req.Add ("sender", sender);
            req.Add ("recipients", recipients);
            req.Add ("subject", subject);
            req.Add ("body", text);
            if(options != null) {
                req.Add("options", options.toJSON());
            }

            byte[] data = getWWWFormData (req);
            HttpWebRequest wr = Mailgun.OpenRequest (messagesUrl ("txt", servername), "POST");
            wr.ContentType = "application/x-www-form-urlencoded";
            wr.ContentLength = data.Length;
            using (Stream rs = Mailgun.GetRequestStream (wr))
                rs.Write (data, 0, data.Length);
            
            using (Mailgun.SendRequest (wr)) {
            }
        }

        public static void SendRaw (string sender, string recipients, byte[] rawMime)
        {
            SendRaw (sender, recipients, rawMime, "");
        }

        /// <summary>
        /// Send raw mime message 
        /// </summary>
        /// <param name="sender">sender specification</param>
        /// <param name="recipients">comma- or semicolon-separated list of recipient specifications</param>
        /// <param name="rawMime">mime-encoded message body</param>
        /// <param name="servername">sending server (can be empty, use 'best' server)</param>
        public static void SendRaw (string sender, string recipients, byte[] rawMime, string servername)
        {
            HttpWebRequest wr = Mailgun.OpenRequest (messagesUrl ("mime", servername), "POST");
            byte[] req = Encoding.UTF8.GetBytes (string.Format ("{0}\n{1}\n\n", sender, recipients));
            wr.ContentLength = req.Length + rawMime.Length;
            wr.ContentType = "text/plain";
            using (Stream rs = Mailgun.GetRequestStream (wr)) {
                rs.Write (req, 0, req.Length);
                rs.Write (rawMime, 0, rawMime.Length);
            }
            using (Mailgun.SendRequest (wr)) {
            }
        }

        protected static string messagesUrl (string format, string servername)
        {
            return Mailgun.ApiUrl + "messages." + format + "?servername=" + servername;
        }

        protected static byte[] getWWWFormData (NameValueCollection reqParams)
        {
            StringBuilder sb = new StringBuilder ();
            for (int i = 0; i < reqParams.Count; ++i) {
                sb.Append (reqParams.GetKey (i));
                sb.Append ('=');
                sb.Append (HttpUtility.UrlEncode (reqParams.Get (i), Encoding.UTF8));
                sb.Append ('&');
            }
            return Encoding.ASCII.GetBytes (sb.ToString ());
        }
    }
    
}
