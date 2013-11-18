using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using OAuth;

// internal class for handling network operations (?)
public class NetworkQueue : AsyncQueue<NetworkQueue, NetworkQueue.Request, NetworkQueue.Response> {
	// Public struct filled out to make a network request.
	public class Request : BaseRequest {
		public string url = null;
		public string method = null;
		public bool useOAuth = false;
		public string oauthKey = null;
		public string oauthKeySecret = null;
		public string oauthToken = null;
		public string oauthTokenSecret = null;
		public bool verifyServerCert = false;
		public string serverCert;
		public Dictionary<string,string> headers = new Dictionary<string,string>();
		public string body = "";
		public object userArg = null;
		public bool trace = false;
		public bool profile = false;
		
		public bool retry = true;
		public int retries;
		
		public float showLoadingPopupDelay = 3f;
		public int loadingPopupId;
		
		public bool fatalOnError = true;
		
		// Probability to have a long delay [0,1]
		public float debugLongDelayProb = 0;
		
		// If a long delay is going to happen, then how many seconds is it going to be [0,flat.MAX_VALUE]
		public float debugLongDelaySeconds = 30f;
		
		// Probability of not reaching the server [0,1]
		public float debugNotReachServerProb = 0;
		
		// Probability of not receiving an answer from the server [0,1]
		public float debugLoseResponseProb = 0;
		
		// Probability to force an API Error from the server [0,1]
		public float debugAPIErrorProb = 0;
		
		public string debugResponse;
		
		public Request(string url, string method) {
			this.url = url;
			this.method = method;
			this.trace = true;
			ResetState();
		}
		
		public void OAuth(string oauthKey, string oauthKeySecret, string oauthToken, string oauthTokenSecret) {
			this.useOAuth = true;
			this.oauthKey = oauthKey;
			this.oauthKeySecret = oauthKeySecret;
			this.oauthToken = oauthToken;
			this.oauthTokenSecret = oauthTokenSecret;
		}
		
		public void VerifyServerCert(string serverCert) {
			this.verifyServerCert = true;
			this.serverCert = serverCert;
		}
		
		public void Callback(NetworkQueue.Callback callback) {
			this.callback = callback;
		}
		
		public void ResetState() {
			this.retries = 3; //AppConfig.networkRetries;
			this.queued = false;
		}
	};
	
	public class TutorialRequest : Request {
		public string fakeJsonResponse;
		
		public TutorialRequest () : base(null,null) {}
	}
	
	// Public struct that contains the response to a network request.
	public class Response : BaseResponse {
		public string error = null;
		public HttpStatusCode httpStatusCode = HttpStatusCode.Unused;
		public string httpStatusDescription = "Unused";
		public Dictionary<string,string> headers = new Dictionary<string,string>();
		public string bodyString = null;
		public byte[] bodyBytes = null;
	};
	
	private static string remoteServerCert = null;
	
	private static bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors policyErrors) {
		// If remoteServerCert is set, then enforce that it matches certificate.
		if(remoteServerCert != null) {
			if(remoteServerCert == certificate.GetPublicKeyString())
				return true;
			else
				return false;
		} else {
			if(policyErrors == SslPolicyErrors.None)
				return true;
			else
				return false;
		}
	}

	private static NetworkQueue _instance;

	public static NetworkQueue instance {
		get {
			if (_instance == null) {
				_instance = new NetworkQueue();
			}
			return _instance;
		}
	}

	private NetworkQueue() {
		// Provide our own SSL validation callback.
		ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateRemoteCertificate);
	}
		
	protected static void Debug(string message, params object[] args) {
		Console.WriteLine(message, args);
	}
	
	protected static void Info(string message, params object[] args) {
		Console.WriteLine(message, args);
	}
	
	protected override void ProcessRequest(Request request, Response response) {
		TutorialRequest tutorialRequest = request as TutorialRequest;
		if (tutorialRequest != null)
			ProcessTutorialRequest(tutorialRequest, response);
		else {
			if (!string.IsNullOrEmpty(request.debugResponse))
				ProcessDebugNetworkRequest(request, response);
			else
				ProcessNetworkRequest(request, response);
		}
			
	}
	
	private void ProcessDebugNetworkRequest(Request request, Response response) {
		Debug("NetworkQueue.ProcessDebugNetworkRequest");
		response.bodyString = request.debugResponse;
	}
	
	private void ProcessTutorialRequest(TutorialRequest request, Response response) {
		Debug("NetworkQueue.ProcessTutorialRequest");
		Debug("Tutorial fake JSON response = " + request.fakeJsonResponse);
		response.bodyString = request.fakeJsonResponse;
	}
		
	private void ProcessNetworkRequest(Request request, Response response) {
		try {
			long startTime = 0;
			if(request.profile) {
				startTime = System.Diagnostics.Stopwatch.GetTimestamp();
			}
			long lastTime = startTime;
			
			// Enable cert verification if it was requested.
			if(request.verifyServerCert)
				remoteServerCert = request.serverCert;
			else
				remoteServerCert = null;
			
			string url = request.url;
			// Add body as url params if doing a GET with a body
			if(request.method == "GET" && !string.IsNullOrEmpty(request.body)) {
				url = url + "?" + request.body;
			}
			
			// Build the request.
			HttpWebRequest httpRequest = WebRequest.Create(url) as HttpWebRequest;
			httpRequest.Method = request.method;
			httpRequest.Timeout = 15000;
			httpRequest.Headers["Accept-Encoding"] = "gzip";
			
			if(request.trace) {
				Info("NetworkQueue Request {0}", DateTime.Now.ToString("HH:mm:ss tt"));
				Info("- {0} {1}", request.method, request.url);
				Info("- Headers:");
			}
			if(request.profile) {
				long currentTime = System.Diagnostics.Stopwatch.GetTimestamp();
				Info("Request allocation: " + (currentTime - lastTime) / 10000000.0f);
				lastTime = currentTime;
			}
			
			// Add headers.
			foreach(KeyValuePair<string,string> kvp in request.headers) {
				// NOTE: Certain headers can not be set this way. If we need to support those headers,
				// then we need to check the key for known values and assign the headers using dedicated setter.
				// http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.headers.aspx
				string key = kvp.Key;
				string val = kvp.Value;
				switch(key) {
				case "Accept":
					httpRequest.Accept = val;
					break;
				default:
					httpRequest.Headers[key] = val;
					break;
				}
				
				if(request.trace) {
					Info("- | {0}: {1}", key, val);
				}
			}
			
			if(request.profile) {
				long currentTime = System.Diagnostics.Stopwatch.GetTimestamp();
				Info("Request headers: " + (currentTime - lastTime) / 10000000.0f);
				lastTime = currentTime;
			}
			
			// Add oauth header if requested.
			if(request.useOAuth) {
				string nonce = OAuth.Manager.GenerateNonce();
				string timestamp = OAuth.Manager.GenerateTimeStamp();
				Uri uri = new Uri(url);
				string signature = OAuth.Manager.GenerateSignature(uri, request.oauthKey, request.oauthKeySecret, request.oauthToken, request.oauthTokenSecret, request.method, timestamp, nonce);
				string authzHeader = OAuth.Manager.GenerateAuthorizationHeader(uri, request.oauthKey, request.oauthToken, timestamp, nonce, "HMAC-SHA1",signature);
				httpRequest.Headers.Add("Authorization", authzHeader);
			
				if(request.trace) {
					string baseString = OAuth.Manager.GenerateSignatureBase(uri, request.oauthKey, request.oauthToken, request.oauthTokenSecret, request.method, timestamp, nonce, "HMAC-SHA1");
					
					Info("- | Authorization: " + authzHeader);
					Info("- BaseString: " + baseString);
					Info("- OAuthKey: " + request.oauthKey + " / " + request.oauthKeySecret);
					Info("- OAuthKToken: " + request.oauthToken + " / " + request.oauthTokenSecret);
				}	
				if(request.profile) {
				long currentTime = System.Diagnostics.Stopwatch.GetTimestamp();
					Info("Request oauth: " + (currentTime - lastTime) / 10000000.0f);
				}
			}
			
			// Write body if present and not doing a GET.
			if(request.method != "GET" && !string.IsNullOrEmpty(request.body)) {
				httpRequest.ContentType = "application/x-www-form-urlencoded";
				byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(request.body);
				httpRequest.ContentLength = bodyBytes.Length;
				using(Stream requestStream = httpRequest.GetRequestStream()) {
		        	requestStream.Write(bodyBytes, 0, bodyBytes.Length);
				}
			}
			
			if(request.trace) {
				Info("- Body: " + request.body);
			}
			if(request.profile) {
				long currentTime = System.Diagnostics.Stopwatch.GetTimestamp();
				Info("Request body: " + (currentTime - lastTime) / 10000000.0f);
				lastTime = currentTime;
			}
			
			// Make the request.
			using (HttpWebResponse httpResponse = httpRequest.GetResponse() as HttpWebResponse) {
				response.httpStatusCode = httpResponse.StatusCode;
				response.httpStatusDescription = httpResponse.StatusDescription;
				
				if(request.trace) {
					Info("NetworkQueue Response");
					Info("- Status: " + response.httpStatusDescription);
					Info("- Headers:");
					Info("- | ContenLength: " + httpResponse.ContentLength);
					Info("- | ContenType: " + httpResponse.ContentType);
				}
				if(request.profile) {
					long currentTime = System.Diagnostics.Stopwatch.GetTimestamp();
					Info("Response get: " + (currentTime - lastTime) / 10000000.0f);
					lastTime = currentTime;
				}
				
				// Get response headers.
				// NOTE: Certain headers can not be retreived this way. If we need to support those headers,
				// then we need to manually add each of them from the httpResponse getters.
				// http://msdn.microsoft.com/en-us/library/system.net.httpwebresponse.headers.aspx
				for(int i=0; i < httpResponse.Headers.Count; ++i) {
					string key = httpResponse.Headers.Keys[i];
					string val = httpResponse.Headers[i];
					response.headers[key] = val;
					
					if(request.trace) {
						Info("- | " + key + ": " + val);
					}
				}
				
				if(request.profile) {
					long currentTime = System.Diagnostics.Stopwatch.GetTimestamp();
					Info("Response headers: " + (currentTime - lastTime) / 10000000.0f);
					lastTime = currentTime;
				}
				
				// If there was no http error, get response body.
				if(response.httpStatusCode == HttpStatusCode.OK) {
					Stream responseStream = httpResponse.GetResponseStream();
					if (httpResponse.ContentEncoding.ToLower() == "gzip") {
						// Debug("Using GZIP");
						responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
					}
					
					if(httpResponse.ContentType == "application/octet-stream") {
						using(BinaryReader streamReader = new BinaryReader(responseStream)) {
							response.bodyBytes = streamReader.ReadBytes((int)httpResponse.ContentLength);
						}
					} else {
						using(StreamReader streamReader = new StreamReader(responseStream, System.Text.Encoding.GetEncoding(httpResponse.CharacterSet))) {
							response.bodyString = streamReader.ReadToEnd();
						}
					
						if(request.trace) {
							Info ("- Body");
							//Info(response.bodyString);
						}
					}
				} else {
					response.error = "Http error: " + response.httpStatusCode;
				}
				httpResponse.Close();
				
				if(request.profile) {
					long currentTime = System.Diagnostics.Stopwatch.GetTimestamp();
					Info("Response body: " + (currentTime - lastTime) / 10000000.0f);
					Info("Total: " + (currentTime - startTime) / 10000000.0f);
				}

				request.callback(response);
			}
		}
		catch(Exception e) {
			if (request.retry && --request.retries >= 0) {
				WebException we = e as WebException;
				if(we != null) {
					Console.WriteLine("Connection error: {0}, will retry {1} times.", we.Status.ToString(), request.retries+1);
				}
				else {
					Console.WriteLine("Exception: {0}, will retry {1} times.", e.Message, request.retries+1);
					Console.WriteLine(e.StackTrace);
				}
				ProcessRequest(request,response);
				return;
			} else {
				response.error = "Exception: " + e.Message;
			}
		}
	}
	
	private System.Random rand;
	private float RandomFloat() {
		if (rand == null)
			rand = new System.Random();
		float ret= (float)rand.NextDouble();
		Debug("RandomFloat: " + ret);
		
		return ret;
	}
}
