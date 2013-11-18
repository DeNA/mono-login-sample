using System.Collections.Generic;
using System.Threading;
using System;

// base class for a singleton queue that will do a given task on a separate thread and respond back on the main thread.
public class AsyncQueue<AsyncQueueType, RequestType, ResponseType>
	where AsyncQueueType: class where RequestType: AsyncQueue<AsyncQueueType, RequestType, ResponseType>.BaseRequest where ResponseType: AsyncQueue<AsyncQueueType, RequestType, ResponseType>.BaseResponse, new() {

	protected AsyncQueue() {}

	public delegate void Callback(ResponseType response);
	
	public class BaseRequest {
		public bool cancelled = false;
		public bool queued = false;
		public bool prioritized = false;
		public Callback callback = null;
	}
	
	public class BaseResponse {
		public RequestType request = null;
	}
	
	// Queues and threads.
	private Queue<RequestType> requestQueue = new Queue<RequestType>();
	private Queue<RequestType> priorityRequestQueue = new Queue<RequestType>();
	private Queue<ResponseType> responseQueue = new Queue<ResponseType>();
	
	private Thread thread = null;
	private int outstandingCount = 0;
	
	public virtual void Start() {
		// Create the thread.
		thread = new Thread(ThreadFunc);
		thread.Priority = System.Threading.ThreadPriority.BelowNormal;
		Console.WriteLine("New Thread Created with name: {0} and id: {1}",thread.Name, thread.ManagedThreadId);
		thread.Name = GetType().Name + "Thr";
		thread.Start();
	}
	
	void Update () {
		if(outstandingCount > 0) {
			ResponseType[] responses;
			lock(responseQueue) {
				int count = responseQueue.Count;
				if(count == 0) return;
				
				responses = new ResponseType[count];
				responseQueue.CopyTo(responses, 0);
				responseQueue.Clear();
			}
			
			for(int i = 0; i < responses.Length; ++i) {
				ResponseType response = responses[i];
				if(response.request.callback != null && !response.request.cancelled)
					response.request.callback(response);
			}
			
			System.Threading.Interlocked.Add(ref outstandingCount, -responses.Length);
		}
	}
	
	// Request a network operation.
	public void Enqueue(RequestType request) {
		if(request.queued)
			throw new System.Exception("This request has already been queued");
		request.queued = true;
		
		lock(requestQueue) {
			if (request.prioritized) {
				priorityRequestQueue.Enqueue(request);
			} else {
				requestQueue.Enqueue(request);
			}
			
			Monitor.Pulse(requestQueue);
		}
	}
	
	public bool Contains(RequestType req) {
		lock(requestQueue) {
			if (req.prioritized) {
				return priorityRequestQueue.Contains(req);
			} else {
				return requestQueue.Contains(req);
			}
		}
	}
	
	// Cancels a previous request. The operation may or may not be executed on the background thread,
	// however, the callback will never be made to the application.
	public void Cancel(RequestType request) {
		request.cancelled = true;
	}
	
	public void CancelAll() {
		lock (requestQueue) {
			foreach(RequestType req in requestQueue)
				req.cancelled = true;
		}
	}
	
	private void ThreadFunc() {
		bool failed;
		while(true) {
			failed = false;
			// Get a request from the requestQueue.
			RequestType request;
			lock(requestQueue) {
				// Use a monitor to go to sleep until a request is put onto the queue.
				while(requestQueue.Count == 0 && priorityRequestQueue.Count == 0)
					Monitor.Wait(requestQueue);
				if (priorityRequestQueue.Count > 0) {
					request = priorityRequestQueue.Dequeue();
				} else {
					request = requestQueue.Dequeue();
				}
			}

			// Create a new response.
			ResponseType response = new ResponseType();
			response.request = request;
				
			// Only process this request if it has not been cancelled.
			if(!request.cancelled) {
				try {
					ProcessRequest(request, response);
				} catch (System.Exception e) {
					Console.WriteLine("Unhandled Exception ProcessRequest, no callback is going to be called!. Error: "+ e.ToString());
					failed = true;
				}
				
				if (!failed) {
					// Put the response onto the responseQueue.
					lock(responseQueue) {
						responseQueue.Enqueue(response);
					}
					
					System.Threading.Interlocked.Increment(ref outstandingCount);
				}
			}
		}
	}
	
	protected virtual void ProcessRequest(RequestType request, ResponseType response) {
	}
}
