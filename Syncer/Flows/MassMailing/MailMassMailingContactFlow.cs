using DaDi.Odoo;
using DaDi.Odoo.Models.MassMailing;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows.MassMailing
{
    [StudioModel(Name = "fson.mail_mass_mailing_contact")]
    [OnlineModel(Name = "mail.mass_mailing.contact")]
    [SyncTargetStudio, SyncTargetOnline]
    public class MailMassMailingContactFlow
        : ReplicateSyncFlow
    {
        public MailMassMailingContactFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonmail_mass_mailing_contact>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<fsonmail_mass_mailing_contact>())
            {
                var studioModel = db.Read(new { mail_mass_mailing_contactID = studioID }).SingleOrDefault();

                // Mandatory child list
                RequestChildJob(SosyncSystem.FundraisingStudio, "fson.mail_mass_mailing_list", studioModel.mail_mass_mailing_listID, SosyncJobSourceType.Default);

                // Optional child personemail
                if (studioModel.PersonEmailID.HasValue && studioModel.PersonEmailID > 0)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.PersonEmail", studioModel.PersonEmailID.Value, SosyncJobSourceType.Default);

                // Optional child personemailgruppe
                if (studioModel.PersonEmailGruppeID.HasValue && studioModel.PersonEmailGruppeID > 0)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.PersonEmailGruppe", studioModel.PersonEmailGruppeID.Value, SosyncJobSourceType.Default);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "list_id", "personemail_id", "personemailgruppe_id" });
            var listID = OdooConvert.ToInt32ForeignKey(odooModel["list_id"], allowNull: false);
            var personemailID = OdooConvert.ToInt32ForeignKey(odooModel["personemail_id"], allowNull: true);
            var personemailgruppeID = OdooConvert.ToInt32ForeignKey(odooModel["personemailgruppe_id"], allowNull: true);

            // Mandatory child list
            RequestChildJob(SosyncSystem.FSOnline, "mail.mass_mailing.list", listID.Value, SosyncJobSourceType.Default);

            // Optional child email
            if (personemailID.HasValue && personemailID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "frst.personemail", personemailID.Value, SosyncJobSourceType.Default);

            // Optional child email group
            if (personemailgruppeID.HasValue && personemailgruppeID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "frst.personemailgruppe", personemailgruppeID.Value, SosyncJobSourceType.Default);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<fsonmail_mass_mailing_contact, mailMassMailingContact>(
                studioID,
                action,
                x => x.mail_mass_mailing_contactID,
                (studio, online) =>
                {
                    int? personemailID = null;

                    if (studio.PersonEmailID.HasValue)
                    {
                        personemailID = GetOnlineID<dboPersonEmail>(
                            "dbo.PersonEmail",
                            "frst.personemail",
                            studio.PersonEmailID.Value);
                    }

                    int? personemailgruppeID = null;

                    if (studio.PersonEmailGruppeID.HasValue)
                    {
                        personemailgruppeID = GetOnlineID<dboPersonEmailGruppe>(
                            "dbo.PersonEmailGruppe",
                            "frst.personemailgruppe",
                            studio.PersonEmailGruppeID.Value);
                    }

                    var listID = GetOnlineID<fsonmail_mass_mailing_list>(
                        "fson.mail_mass_mailing_list",
                        "mail.mass_mailing.list",
                        studio.mail_mass_mailing_listID);

                    var countryID = GetCountryIdForLandId(studio.LandID);

                    online.Add("list_id", listID);
                    online.Add("personemail_id", personemailID);
                    online.Add("personemailgruppe_id", personemailID);

                    online.Add("firstname", studio.firstname);
                    online.Add("lastname", studio.lastname);
                    online.Add("gender", studio.gender);
                    online.Add("anrede_individuell", studio.anrede_individuell);
                    online.Add("title_web", studio.title_web);
                    online.Add("birthdate_web", studio.birthdate_web);
                    online.Add("newsletter_web", studio.newsletter_web);
                    online.Add("phone", studio.phone);
                    online.Add("email", studio.email);
                    online.Add("mobile", studio.mobile);

                    online.Add("street", studio.street);
                    online.Add("street2", studio.street2);
                    online.Add("street_number_web", studio.street_number_web);
                    online.Add("zip", studio.zip);
                    online.Add("city", studio.city);
                    online.Add("state_id", studio.state_id);
                    online.Add("country_id", countryID);

                    online.Add("bestaetigt_am_um", studio.BestaetigtAmUm);
                    online.Add("bestaetigt_herkunft", studio.BestaetigungsHerkunft);
                    online.Add("bestaetigt_typ", Svc.TypeService.GetTypeValue(studio.BestaetigungsTypID));
                    online.Add("opt_out", studio.opt_out);
                    online.Add("renewed_subscription_date", studio.renewed_subscription_date);
                    online.Add("renewed_subscription_log", studio.renewed_subscription_log);
                    // Do not set "state"!
                    online.Add("origin", studio.origin);
                    online.Add("gdpr_accpeted", studio.DSGVOZugestimmt);

                    online.Add("pf_mandatsid", studio.pf_mandatsid);
                    online.Add("pf_zahlungsreferenz", studio.pf_zahlungsreferenz);
                    online.Add("pf_bpknachname", studio.pf_bpknachname);
                    online.Add("pf_formularnummer", studio.pf_formularnummer);
                    online.Add("pf_email", studio.pf_email);
                    online.Add("pf_xguid", studio.pf_xguid);
                    online.Add("pf_patenkindvorname", studio.pf_patenkindvorname);
                    online.Add("pf_naechstevorlageam", studio.pf_naechstevorlageam);
                    online.Add("pf_name", studio.pf_name);
                    online.Add("pf_anredelower", studio.pf_anredelower);
                    online.Add("pf_jahr", studio.pf_jahr);
                    online.Add("pf_bank", studio.pf_bank);
                    online.Add("pf_wunschspendenbetrag", studio.pf_wunschspendenbetrag);
                    online.Add("pf_iban", studio.pf_iban);
                    online.Add("pf_titelnachname", studio.pf_titelnachname);
                    online.Add("pf_anredekurz", studio.pf_anredekurz);
                    online.Add("pf_personid", studio.pf_personid);
                    online.Add("pf_nameschenker", studio.pf_nameschenker);
                    online.Add("pf_iban_verschluesselt", studio.pf_iban_verschluesselt);
                    online.Add("pf_patentier", studio.pf_patentier);
                    online.Add("pf_patenkind", studio.pf_patenkind);
                    online.Add("pf_emaildatum", studio.pf_emaildatum);
                    online.Add("pf_anredelang", studio.pf_anredelang);
                    online.Add("pf_vorname", studio.pf_vorname);
                    online.Add("pf_bpkvorname", studio.pf_bpkvorname);
                    online.Add("pf_namebeschenkter", studio.pf_namebeschenkter);
                    online.Add("pf_jahresbeitrag", studio.pf_jahresbeitrag);
                    online.Add("pf_bpkgeburtsdatum", studio.pf_bpkgeburtsdatum);
                    online.Add("pf_bic", studio.pf_bic);
                    online.Add("pf_teilbeitrag", studio.pf_teilbeitrag);
                    online.Add("pf_naechstevorlageammonatjahr", studio.pf_naechstevorlageammonatjahr);
                    online.Add("pf_bpkplz", studio.pf_bpkplz);
                    online.Add("pf_geburtsdatum", studio.pf_geburtsdatum);
                    online.Add("pf_betragspendenquittung", studio.pf_betragspendenquittung);
                    online.Add("pf_zahlungsintervall", studio.pf_zahlungsintervall);
                    online.Add("pf_anrede", studio.pf_anrede);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<mailMassMailingContact, fsonmail_mass_mailing_contact>(
                onlineID,
                action,
                x => x.mail_mass_mailing_contactID,
                (online, studio) =>
                {
                    var personemailID = GetStudioIDFromOnlineReference(
                        "dbo.PersonEmail",
                        online,
                        x => x.personemail_id,
                        false);

                    var personemailGruppeID = GetStudioIDFromOnlineReference(
                        "dbo.PersonEmailGruppe",
                        online,
                        x => x.personemailgruppe_id,
                        false);

                    var listID = GetStudioIDFromOnlineReference(
                        "fson.mail_mass_mailing_list",
                        online,
                        x => x.list_id,
                        true);

                    var landID = GetLandIdForCountryId(
                        OdooConvert.ToInt32ForeignKey(online.country_id, allowNull: true));

                    studio.mail_mass_mailing_listID = listID.Value;
                    studio.PersonEmailID = personemailID;
                    studio.PersonEmailGruppeID = personemailGruppeID;

                    studio.firstname = online.firstname;
                    studio.lastname = online.lastname;
                    studio.gender = online.gender;
                    studio.anrede_individuell = online.anrede_individuell;
                    studio.title_web = online.title_web;
                    studio.birthdate_web = online.birthdate_web;
                    studio.newsletter_web = online.newsletter_web;
                    studio.email = online.email;
                    studio.phone = online.phone;
                    studio.mobile = online.mobile;
                    studio.street = online.street;
                    studio.street2 = online.street2;
                    studio.street_number_web = online.street_number_web;
                    studio.zip = online.zip;
                    studio.city = online.city;
                    studio.state_id = OdooConvert.ToInt32ForeignKey(online.state_id, true);
                    studio.LandID = landID;

                    studio.BestaetigtAmUm = online.bestaetigt_am_um;
                    studio.BestaetigungsHerkunft = online.bestaetigt_herkunft;
                    studio.BestaetigungsTypID = Svc.TypeService.GetTypeID("fsonmail_mass_mailing_contact_BestaetigungsTypID", online.bestaetigt_typ);
                    studio.renewed_subscription_date = online.renewed_subscription_date;
                    studio.renewed_subscription_log = online.renewed_subscription_log;
                    studio.opt_out = online.opt_out;
                    studio.state = online.state;
                    studio.origin = online.origin;
                    studio.DSGVOZugestimmt = online.gdpr_accepted;

                    studio.pf_mandatsid = online.pf_mandatsid;
                    studio.pf_zahlungsreferenz = online.pf_zahlungsreferenz;
                    studio.pf_bpknachname = online.pf_bpknachname;
                    studio.pf_formularnummer = online.pf_formularnummer;
                    studio.pf_email = online.pf_email;
                    studio.pf_xguid = online.pf_xguid;
                    studio.pf_patenkindvorname = online.pf_patenkindvorname;
                    studio.pf_naechstevorlageam = online.pf_naechstevorlageam;
                    studio.pf_name = online.pf_name;
                    studio.pf_anredelower = online.pf_anredelower;
                    studio.pf_jahr = online.pf_jahr;
                    studio.pf_bank = online.pf_bank;
                    studio.pf_wunschspendenbetrag = online.pf_wunschspendenbetrag;
                    studio.pf_iban = online.pf_iban;
                    studio.pf_titelnachname = online.pf_titelnachname;
                    studio.pf_anredekurz = online.pf_anredekurz;
                    studio.pf_personid = online.pf_personid;
                    studio.pf_nameschenker = online.pf_nameschenker;
                    studio.pf_iban_verschluesselt = online.pf_iban_verschluesselt;
                    studio.pf_patentier = online.pf_patentier;
                    studio.pf_patenkind = online.pf_patenkind;
                    studio.pf_emaildatum = online.pf_emaildatum;
                    studio.pf_anredelang = online.pf_anredelang;
                    studio.pf_vorname = online.pf_vorname;
                    studio.pf_bpkvorname = online.pf_bpkvorname;
                    studio.pf_namebeschenkter = online.pf_namebeschenkter;
                    studio.pf_jahresbeitrag = online.pf_jahresbeitrag;
                    studio.pf_bpkgeburtsdatum = online.pf_bpkgeburtsdatum;
                    studio.pf_bic = online.pf_bic;
                    studio.pf_teilbeitrag = online.pf_teilbeitrag;
                    studio.pf_naechstevorlageammonatjahr = online.pf_naechstevorlageammonatjahr;
                    studio.pf_bpkplz = online.pf_bpkplz;
                    studio.pf_geburtsdatum = online.pf_geburtsdatum;
                    studio.pf_betragspendenquittung = online.pf_betragspendenquittung;
                    studio.pf_zahlungsintervall = online.pf_zahlungsintervall;
                    studio.pf_anrede = online.pf_anrede;
                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
