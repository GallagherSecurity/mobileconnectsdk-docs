const express = require('express');
const router = express.Router();

module.exports = function (usingDatabase, gglApi, commandCentreConfig) {

  router.get("/", (req, res) => {
    res.render('students/index', { studentId: '' });
  });

  // logon stuff, just like a real website
  router.post('/login', (req, res) => {
    usingDatabase(async db => {
      const student = await db.getAsync("SELECT id, username from students where studentId = ? and password = ?;", req.body.studentId, req.body.password);
      if (!student) {
        res.render('students/index', { studentId: req.body.studentId, error: "Cannot find specified StudentId/Password combination" });
        return;
      }

      res.redirect('/students/main?id='+student.id);
    });
  });

  // Load the main student page, which lists their mobile credentials
  router.get('/main', async (req, res) => {
    usingDatabase(async db => {
      let error = null;
      const student = await db.getAsync("SELECT id, username, studentId from students where id = ?", req.query.id);
      if(!student) {
        res.redirect('/students');
        return;
      }

      // Search for the cardholder using the student ID personal data field.
      // Note: when searching for things in the command centre REST api, quote the value for exact match
      const url = `${commandCentreConfig.cardholderSearchHref}?pdf_${commandCentreConfig.keyPersonalDataFieldId}="${student.studentId}"`
      const cardholderSearchResponse = await gglApi.getAsync(url);

      // Important design consideration:
      // There will be a representation of the person (cardholder) in both your external system, and also in command centre.
      // You'll need to somehow align these two records.
      //
      // The conventional (pre-rest api) way this is done is to attach a personal data field to the cardholder in command centre, and give it a 
      // value that exists in your external system, such as a "Student ID" or "Employee Number" or some other thing like that.
      // Then you can use this value to look up the cardholder record.
      // Note however that this relies on something putting that external ID into command centre somehow (for example, using one
      // of command centre's other cardholder import mechanisms)
      //
      // An alternative way is that if you use the REST api to *create* the cardholder in the first place, then as part of that
      // creation, you can store its href or numeric ID in your external system. 
      // If you store it's href, then you can simply go and load/edit the cardholder record without first having 
      // to do a search using some other parameter which is probably more convenient.
      // Note that this relies on you creating (for example) a column in your own database to store the Command Centre Cardholder Href
      //
      // Because this is a sample app, we do both (why not?!)
      // How this works, is when you load the 'main' page, we look up the cardholder href in command centre
      // and if we find it, we store the href in our own sqlite database.
      // That way, when we want to request a mobile credential, we can skip the search-by-pdf step
      let credentials = [];
      let cardholderDetails = null;

      if (cardholderSearchResponse.body['results'] && cardholderSearchResponse.body.results.length == 1) {
        const cardholderHref = cardholderSearchResponse.body.results[0].href;
        await db.runAsync("UPDATE students SET commandCentreHref = ? where id = ?", cardholderHref, student.id);

        // do a GET on the cardholder's HREF to pull back all the details
        cardholderDetails = await gglApi.getAsync(cardholderHref);

        if (cardholderDetails.body['cards']) {
          const cards = cardholderDetails.body.cards
          // only mobile credentials have 'invitation'
          credentials = cards.filter(card => card["invitation"]);
        }

      } else {
        error = "Cannot find your cardholder record in command centre" // stay on the page, just show an error
      }

      res.render('students/main', { student: student, credentials: credentials, cardholderDetails: cardholderDetails.body, error: error, message: req.query.message });
    });
  });

  router.post('/deleteMobileCredential', async (req, res) => {
    const deleteResponse = await gglApi.deleteAsync(req.body.credentialHref);
    if (deleteResponse.statusCode == 204) { // success! There is no response body.
      res.redirect(`/students/main?id=${req.body.userId}&message=Deleted Credential`)
      return;
    }
    // else we failed, try read the body
    const message = ("body" in deleteResponse && "message" in deleteResponse.body) ? deleteResponse.body.message : deleteResponse.statusMessage;
    res.redirect(`/students/main?id=${req.body.userId}&error=${message}`);
  });

  router.post('/requestMobileCredential', async (req, res) => {
    usingDatabase(async db => {
      const student = await db.getAsync("SELECT id, username, studentId, commandCentreHref from students where id = ?", req.body.userId);
      // If we hadn't stored the student's commandCentreHref, we could look it up based on student ID, but this would
      // involve an extra step (see above)
      if (!student || !student.commandCentreHref) {
        // If we hadn't stored the student's commandCentreHref, we could look it up by doing a cardholder search for the student's StudentId personal data field
        res.redirect(`/students/main?id=${req.body.userid}&error=No command centre href is saved for that student!`);
        return;
      }

      // if we're registering with an email/sms or register second factor flag, pass those along
      let invitation = {}
      if (req.body.email) {
        invitation["email"] = req.body.email;
      }
      if (req.body.sms) {
        invitation["mobile"] = req.body.sms;
      }
      if (req.body.singleFactorOnly == 'on') {
        invitation["singleFactorOnly"] = true;
      }

      let cardToAdd = { "type": { "href": commandCentreConfig.mobileCredentialTypeHref } };
      if (Object.keys(invitation).length > 0) {
        cardToAdd["invitation"] = invitation
      }

      const patchCardholderResponse = await gglApi.patchAsync(student.commandCentreHref, {
        "cards": {
          "add": [
            cardToAdd // add a single card of the configured mobile credential card type
          ],
        }
      });

      if (patchCardholderResponse.statusCode == 204) { 
        // success. There is no response body.
        // If you want to react immediately to the creation of the mobile credential,
        // you must load the cardholder details and look for a mobile credential with invitation.status of 'sent' and a non-null invitation.href 
        // We need a polling loop as the invitation.href won't be there immediately, it arrives after the Command Centre server has sent a message, and received
        // a response from the Gallagher Cloud. This is typically quite fast, however it is dependent on the latency of the internet connection between command centre
        // and the Gallagher Cloud, so won't be instant.
        // We do that here to illustrate how you might create such a polling loop.
        //
        // HOWEVER, It is highly likely you don't actually need to do this at all. Simply create a credential and forget about the response (other than checking for HTTP errors and such).
        // The next time your mobile app connects to your service (e.g. on logon or resume), AT THAT POINT you can query the Command centre REST api for any 'sent' credentials and
        // obtain the invitation href.
        // This is also better because if the credential gets delayed (e.g. command centre can't talk to the cloud) then it'll be fine.
        // - at some point in future the credential href will come back from the cloud, and your mobile app can pick it up then.
        // If you do need quick feedback, you could also implement the polling loop in the mobile app rather than in the server which is probably nicer.
        //
        // Minor Note: Astute readers might be thinking "What if I add two credentials at the same time? How will I know which one is which?"
        // The answer is threefold:
        //
        // - This is not recommended. It's not a particularly useful thing to do.
        //
        // - Provided you remember to filter for credentials of the same card type you're creating with (as we do below), then it actually doesn't
        //   matter which credential is which, as they should both be functionally identical and it's unlikely you could tell if you got the right one or not
        //
        // - If you legitimately want to do this, you can pass a "number" when you add the credential, which is arbitrary text of your chosing.
        //   You can then use that "number" to filter for credentials and ensure you find the right one.
        //   Note: The intent of the "number" field is to put a user-friendly name for the device, such as "Samsung Galaxy S6" so people with two phones
        //   can tell which credential goes where, but you can put anything in it, such as a UUID or unique integer.
        const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

        for (let i = 0; i < 10; i++) {
          const cardholderResponse = await gglApi.getAsync(student.commandCentreHref);

          if (cardholderResponse.body['cards']) {
            const pendingCredentials = cardholderResponse.body.cards.filter(card => 
              card.type.href == commandCentreConfig.mobileCredentialTypeHref && 
              card["invitation"] && 
              card.invitation.status == 'sent' 
              && card.invitation["href"] != ''); // JS empty-string equality check also tests for null

            if (pendingCredentials.length > 0) { // we found our newly-added credential
              res.render('students/requestMobileCredential', { credential: pendingCredentials[0], student: student });
              return;
            }
            await sleep(500); // you could possibly be more agressive here; a sleep 100 or sleep 200 might be fine depending on how good your internet is.
          }
        }

        // if we arrive here it means we've tried 10 times and still have no result; We have to give up at some point
        res.redirect(`/students/main?id=${student.id}&error=Timed out trying to create a mobile credential! Command Centre may not be able to communicate with the Gallagher Cloud!`);
        return;

      } 
      // else we failed, try read the body
      const message = ("body" in deleteResponse && "message" in deleteResponse.body) ? deleteResponse.body.message : deleteResponse.statusMessage;
      res.redirect(`/students/main?id=${req.body.userid}&error=${message}`);
    });
  });

  return router;
}
