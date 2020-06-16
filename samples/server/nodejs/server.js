const express = require("express");
const ejs = require('ejs');
const expressLayouts = require("express-ejs-layouts");
const bodyParser = require("body-parser");
const sqlite3 = require("sqlite3");
const gglApiClientFactory = require("./ggl-api-client.js");
require("./databasehelpers.js");
const config = require("./config.json");
const fs = require('fs');

process.on('unhandledRejection', (reason, p) => {
  console.log('Unhandled Rejection at: Promise', p, 'reason:', reason);
});

// ****************************************************************************
// Setup the webapp (express js)
// ****************************************************************************

const app = express()
app.set('view engine', 'ejs')
app.use(expressLayouts);
app.use(express.static('public'));
app.use(bodyParser.urlencoded({ extended: true }));

// ****************************************************************************
// Setup our sample sqlite database
// ****************************************************************************

// create the data directory if it doesn't yet exist
if (!fs.existsSync('data')){
  console.log("creating 'data' directory");
  fs.mkdirSync('data');
}

// small helper function to open/use/close the database
async function usingDatabase(callback) {
  const db = new sqlite3.Database('data/database.sqlitedb');
  try { await callback(db); }
  catch (err) { 
    console.log(err.message);
    console.log(err.stack); 
  }
  finally { db.close(); }
}

// set up the initial database
usingDatabase(async db => {
  await db.runAsync("CREATE TABLE IF NOT EXISTS students (id INTEGER PRIMARY KEY, username TEXT UNIQUE, studentId TEXT UNIQUE, password TEXT, commandCentreHref TEXT, admin bit)");
  console.log("Created database and students");
});

// ****************************************************************************
// Connect to the REST api entrypoint and load some configuration and base url's
// ****************************************************************************
const gglApi = gglApiClientFactory(config.host, config.apiKey, config.clientCertificate, config.clientCertificatePrivateKey, config.clientCertificatePrivateKeyPassword);

// When searching for cardholders in command centre using a personal data field, we need the numeric
// identifier of the personal data field itself. For example, the "Student ID" pdf may have an id of 500
// So we need to use the "items" feature of the rest api to find that numeric id and store it for later use
var keyPersonalDataFieldId = null;

// When creating new cardholders we need to specify a Division and Access Group to associate them with
// These are provided to the create cardholder call as href links to the item
var studentAccessGroupHref = null;
var rootDivisionHref = null;

// When searching for cardholders in command centre, we do a GET to a given URL.
// In the current release of Command Centre, it's always '/api/cardholders'
// However following the principles of REST and HATEOAS we get this URL by following links from the entrypoint.
// This is also useful as if we're not privileged to load cardholders, the entrypoint won't have the
// feature present, so we'll know that we're not allowed to search for cardholders before we try and start
var cardholderSearchHref = null;

// When adding mobile credentials to cardholders, we need to supply the "card type" href
// Command Centre needs this to associate the credential with a "type" - the "type" has metadata such as
// default enable/disable dates, card states and other things like that. Therefore we need to look it up 
// by name and store the href for later use
var mobileCredentialTypeHref = null;

// Always start with the entrypoint to the Command Centre REST api, which is /api
// can't await in toplevel so we have to use 'then'
gglApi.getAsync('/api').then(async apiResponse => {
  try {
    // we expect a JSON response like this (but with extra things we aren't using in this example):
    // {
    //     "version": "1.2.3.4",
    //     "features": {
    //         "items": {
    //             "items": {
    //                 "href": "https://localhost:8904/api/items"
    //             },
    //         },
    //         "cardholders": {
    //             "cardholders": {
    //                 "href": "https://localhost:8904/api/cardholders"
    //             }
    //         },
    //         "cardTypes": {
    //             "cardTypes": {
    //                 "href": "https://localhost:8904/api/card_types"
    //             }
    //         }
    //     }
    // }

    if (!apiResponse.body['features']) {
      console.log("ERROR: Invalid response from the REST api; 'features' should always be returned from the api entrypoint")
      console.log(apiResponse.body)
      process.exit(1);
    }

    // get the base url for the cardholders feature
    if (!apiResponse.body.features['cardholders']) {
      console.log("ERROR: No cardholders feature in the REST api; Are you licensed for REST cardholders?")
      console.log(apiResponse.body)
      process.exit(1);
    }

    cardholderSearchHref = apiResponse.body.features.cardholders.cardholders.href;
    console.log("Success: Found cardholderSearchHref; " + cardholderSearchHref);
    
    // look up the link for the root division to create new cardholders into
    const itemsSearchHref = apiResponse.body.features.items.items.href;
    const divisionResponse = await gglApi.getAsync(`${itemsSearchHref}?type=15&name="Root Division"`);

    if (!divisionResponse.body['results'] || divisionResponse.body.results.length == 0) {
      console.log("ERROR: Can't find the Root Division!");
      console.log(divisionResponse.body)
      process.exit(1);
    }
    rootDivisionHref = divisionResponse.body.results[0].href;
    console.log(`Success: Found the Root Division; it's link is ${rootDivisionHref}`);

    // go look up the search key for the student ID pdf - type 33 is 'personal data field'
    const configPdfResponse = await gglApi.getAsync(`${itemsSearchHref}?type=33&name="${encodeURIComponent(config.pdfName)}"`); // quoted name for exact string match
    // we expect a JSON response like this:
    // {
    //     "results": [
    //         {
    //             "id": "500",
    //             "name": "Student ID",
    //             "type": {
    //                 "id": "33",
    //                 "name": "Personal Data Field"
    //             }
    //         }
    //     ]
    // }

    if (!configPdfResponse.body['results'] || configPdfResponse.body.results.length == 0) {
      console.log("ERROR: Can't find a personal data field with name of " + config.pdfName);
      console.log(configPdfResponse.body)
      process.exit(1);
    }
    keyPersonalDataFieldId = configPdfResponse.body.results[0].id;
    console.log(`Success: Found PDF called ${config.pdfName}; it's lookup key ID is ${keyPersonalDataFieldId}`);

    // find the Students access group (type == 2) which contains the Student ID pdf
    const configAccessGroupResponse = await gglApi.getAsync(`${itemsSearchHref}?type=2&name="${encodeURIComponent(config.accessGroupName)}"`);

    if (!configAccessGroupResponse.body['results'] || configAccessGroupResponse.body.results.length == 0) {
      console.log("ERROR: Can't find an access group with name of " + config.accessGroupName);
      console.log(configAccessGroupResponse.body)
      process.exit(1);
    }
    studentAccessGroupHref = configAccessGroupResponse.body.results[0].href;
    console.log(`Success: Found Access Group called ${config.accessGroupName}; it's link is ${studentAccessGroupHref}`);

    // go look up the search key for the mobile credential card type
    const cardTypesSearchHref = apiResponse.body.features.cardTypes.cardTypes.href;
    const configCardTypeResponse = await gglApi.getAsync(`${cardTypesSearchHref}?name="${encodeURIComponent(config.cardTypeName)}"`); // quoted name for exact string match
    // we expect a JSON response like this:
    // {
    //     "results": [
    //         {
    //             "href": "https://localhost:8904/api/card_types/471",
    //             "id": "471",
    //             "name": "Mobile Credential",
    //             "credentialClass": "mobile"
    //         },
    //     ]
    // }
    if (!configCardTypeResponse.body['results'] || configCardTypeResponse.body.results.length == 0) {
      console.log("ERROR: Can't find a card type with name of " + config.cardTypeName)
      console.log(configCardTypeResponse.body)
      process.exit(1);
    }
    mobileCredentialTypeHref = configCardTypeResponse.body.results[0].href;
    console.log(`Success: Found card type called ${config.cardTypeName}; it's href is ${mobileCredentialTypeHref}`);

    const commandCentreConfiguration = {
      keyPersonalDataFieldId: keyPersonalDataFieldId,
      keyPersonalDataFieldName: config.pdfName,
      studentAccessGroupHref: studentAccessGroupHref,
      rootDivisionHref: rootDivisionHref,
      cardholderSearchHref: cardholderSearchHref,
      mobileCredentialTypeHref: mobileCredentialTypeHref
    };

    // ****************************************************************************
    // Request Handling
    // ****************************************************************************

    // The main index page just renders it's view and redirects to other places. No need for a controller
    app.get('/', (req, res) => {
      res.render('index');
    });

    const adminController = require('./controllers/admin.js');
    app.use('/admin', adminController(usingDatabase, gglApi, commandCentreConfiguration));

    const studentsController = require('./controllers/students.js');
    app.use('/students', studentsController(usingDatabase, gglApi, commandCentreConfiguration));

  } catch (err) {
    console.log(err);
  }
}, err => {
  console.log(err);
});

app.listen(3000, () => {
  console.log('Example app listening on port 3000!')
});