public static class UriUtility {
	public static string QueryString(params string[] uriParams) {
		if (uriParams.Length%2 != 0) {
			throw new System.Exception("uriParams Length is not divisble by 2. It should have a sequence of key value pairs.");
		}
		
		if (uriParams.Length == 0) {
			return string.Empty;
		}
		
		System.Text.StringBuilder sb = new System.Text.StringBuilder(16 * uriParams.Length);
		for (int i = 0; i < uriParams.Length; i++) {
			sb.Append(System.Uri.EscapeDataString(uriParams[i]));
			sb.Append("=");
			if (!string.IsNullOrEmpty(uriParams[++i]))
				sb.Append(System.Uri.EscapeDataString(uriParams[i]));
			sb.Append("&");
		}
		
		return sb.ToString(0,sb.Length-1);
	}
	
	public static string QueryStringRepeat(string keyName, params string[] uriValues) {
		if (uriValues.Length == 0) {
			return string.Empty;
		}
		
		System.Text.StringBuilder sb = new System.Text.StringBuilder(16 * (uriValues.Length + 1));
		for (int i = 0; i < uriValues.Length; i++) {
			sb.Append(System.Uri.EscapeUriString(keyName));
			sb.Append("=");
			if (!string.IsNullOrEmpty(uriValues[i]))
				sb.Append(System.Uri.EscapeUriString(uriValues[i]));
			sb.Append("&");
		}
		
		return sb.ToString(0,sb.Length-1);
	}
}