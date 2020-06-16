const sqlite3 = require('sqlite3');

function toPromise(method) {
  return function() {
    return new Promise((resolve, reject) => {
      this[method](...arguments, (err, row) => {
        if(row) {
          resolve(row);
        } else if(err) {
          reject(err);
        } else {
          resolve(null); // some sqlite functions don't fail, they just simply don't return any rows (e.g. run)
        }
      });
    });
  }
}

// async wrappers for sqlite3 functions
sqlite3.Database.prototype.getAsync = toPromise('get')
sqlite3.Database.prototype.allAsync = toPromise('all')
sqlite3.Database.prototype.runAsync = toPromise('run')
  