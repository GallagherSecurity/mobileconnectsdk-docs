// rename on import because it's useful to have local variables called url and request
const webRequest = require ("request");
const resolveUrl = require("url").resolve; 
const fs = require("fs");

// A small wrapper around the 'request' npm package.
//
// It adds the Authorization: GGL-API-KEY header to authenticate with Command Centre.
// It configures the TLS client certificate.
// It also turns off SSL server certificate validation (Command Centre will generally run with a self-signed certificate)
// 
// Otherwise it mostly converts things into promises so we can await them
class GglApiClient {
  constructor(baseUrl, apiKey, clientCertificateFile, clientPrivateKeyFile, clientCertificatePrivateKeyPassword) {
    this.baseUrl = baseUrl;
    this.apiKey = apiKey;
    this.clientCertificate = fs.readFileSync(clientCertificateFile);
    this.clientPrivateKey = fs.readFileSync(clientPrivateKeyFile);
    this.clientCertificatePrivateKeyPassword = clientCertificatePrivateKeyPassword;
  }

  getAsync(url) {
    return this.requestAsync("GET", url);
  }

  postAsync(url, body) {
    return this.requestAsync("POST", url, body);
  }

  patchAsync(url, body) {
    return this.requestAsync("PATCH", url, body);
  }

  deleteAsync(url, body) {
    return this.requestAsync("DELETE", url, body);
  }

  requestAsync(method, url, body) {
    const targetUrl = resolveUrl(this.baseUrl, url);
    console.log(`${method} ${targetUrl}`);
    console.log(JSON.stringify(body));

    const requestOptions = {
      method: method,
      uri: targetUrl,
      body: body,
      json: true,
      headers: {
        'Accept': 'application/json',
        'Authorization': `GGL-API-KEY ${this.apiKey}`
      },
      strictSSL: false, // the gallagher server will have a self-signed certificate which won't validate against any certificate authorities. You should pin it's public key instead
      agentOptions: {
        cert: this.clientCertificate,
        key: this.clientPrivateKey,
        passphrase: this.clientCertificatePrivateKeyPassword
      }
    };

    //const https = webRequest('https');

    return new Promise((resolve, reject) => {
      webRequest(requestOptions, (err, res) => {
        if(err) {
          reject(err);
        } else {
          resolve(res);
        }
      });
    });
  }
}

module.exports = function () {
  return new GglApiClient(...arguments);
}