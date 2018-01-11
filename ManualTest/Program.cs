using dadi_data;
using dadi_data.Models;
using Microsoft.Extensions.DependencyInjection;
using Odoo;
using Odoo.Models;
using Syncer.Flows;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WebSosync.Common;

namespace ManualTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var sc = new ServiceCollection();
            var svc = sc.BuildServiceProvider();

            var f = new ProductTemplateFlow(svc);

            Console.WriteLine("Done");
            Console.ReadKey();
        }

        private void LoadTest()
        {
            Console.WriteLine("Started.");

            var prepareServer = GetCurrentdboPersonStack(1);

            long sum = 0;
            for (int i = 1; i <= 7; i++)
            {
                Stopwatch s = new Stopwatch();
                s.Start();
                var m = GetCurrentdboPersonStack(1210972 + i);
                s.Stop();
                Console.WriteLine($"{1210972 + i}: Load time: {s.ElapsedMilliseconds}ms");
                sum += s.ElapsedMilliseconds;
            }
            Console.WriteLine($"Average: Load time: {sum / 7}ms");

            Console.WriteLine("--------------------------------------------------");

            sum = 0;
            for (int i = 1; i <= 7; i++)
            {
                Stopwatch s = new Stopwatch();
                s.Start();
                var m = GetCurrentdboPersonStack_new(1210972 + i);
                s.Stop();
                Console.WriteLine($"{1210972 + i}: Load time: {s.ElapsedMilliseconds}ms");
                sum += s.ElapsedMilliseconds;
            }
            Console.WriteLine($"Average: Load time: {sum / 7}ms");
        }

        private void OdooQuery()
        {
            Stopwatch s = new Stopwatch();
            var client = new OdooClient($"http://wwfa.datadialog.net/xmlrpc/2/", "wwfa");
            client.Authenticate("sosync", "YWGXlaB5cfPUDs9a");

            s.Start();
            var company = client.GetModel<resCompany>("res.company", 1);
            s.Stop();

            Console.WriteLine(client.LastRequestRaw);
        }

        private static void Threads()
        {
            // Give sosync2 server some time to start up
            Thread.Sleep(5000);

            Console.WriteLine("Manual testing...");

            //int count = 200; // 2000 Entries
            //var tasks = new Task[]
            //{
            //    StartRequestsAsync("A", count),
            //    StartRequestsAsync("B", count),
            //    StartRequestsAsync("C", count),
            //    StartRequestsAsync("D", count),
            //    StartRequestsAsync("E", count),
            //    StartRequestsAsync("F", count),
            //    StartRequestsAsync("G", count),
            //    StartRequestsAsync("H", count),
            //    StartRequestsAsync("I", count),
            //    StartRequestsAsync("J", count)
            //};

            int threadCount = 5;
            int count = 100; // 5 x 100 = 500

            for (int ti = 0; ti < threadCount; ti++)
            {
                Thread t = new Thread(new ParameterizedThreadStart((p) => 
                {
                    var pr = (ThreadParam)p;
                    StartRequestsAsync(pr.ID, pr.Nr);
                }));

                t.Start(new ThreadParam() { ID = ((char)(65 + ti)).ToString(), Nr = count });
            }

            //StartRequestsAsync("A", count);
            //StartRequestsAsync("B", count);
            //StartRequestsAsync("C", count);
            //StartRequestsAsync("D", count);
            //StartRequestsAsync("E", count);
            //StartRequestsAsync("F", count);
            //StartRequestsAsync("G", count);
            //StartRequestsAsync("H", count);
            //StartRequestsAsync("I", count);
            //StartRequestsAsync("J", count);

            //Task.WhenAll(tasks);

            Console.WriteLine($"All tasks finished.");
        }

        static void StartRequestsAsync(string identifier, int count)
        {
            //string template = "http://localhost:5050/job/create?job_date={0:yyyy-MM-dd}%20{0:HH:mm:ss.fffffff}&job_source_system=fs&job_source_model=dbo.Person&job_source_record_id=13&job_source_sosync_write_date={0:yyyy-MM-dd}T{0:HH:mm:ss.fffffff}Z";

            //var requests = new List<Task>(count);

            using (var db = new DataService<dboPerson>("Data Source=MSSQL1;Initial Catalog=mdb_demo; Integrated Security=True;"))
            {
                for (int i = 0; i < count; i++)
                {
                    UpdateMssql(identifier, i + 1, db);

                    //var t = Task.Run(() =>
                    //{
                    //    UpdateMssql(myId, myNr);
                    //});

                    //requests.Add(t);

                    // requests.Add(Task.Run(() => UpdateMssql(identifier, i + 1)));
                    //requests.Add(factory.StartNew(() => UpdateMssql(identifier, i + 1)));

                    //var req = HttpWebRequest.Create(String.Format(template, DateTime.UtcNow));
                    //req.Method = "GET";
                    //requests.Add(req.GetResponseAsync());
                }
            }
            //await Task.WhenAll(requests);
        }


        private static Random _rnd = new Random();

        private static void UpdateMssql(string id, int nr, DataService<dboPerson> db)
        {
            Console.WriteLine($"{id}: Updating mssql {nr}...");

            try
            {
                var pers = db.Read(new { PersonID = 13 }).SingleOrDefault();
                pers.Name = $"Reynoldse-{id}-{nr}";
                db.Update(pers);

                Console.WriteLine($"{id}: {nr} is done...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{id}: {nr} error: {ex.Message}");
            }
        }

        private static class MdbService
        {
            public static DataService<TModel> GetDataService<TModel>() where TModel : MdbModelBase, new()
            {
                return new DataService<TModel>("Data Source=MSSQL1;Initial Catalog=mdb_bsvw; Integrated Security=True;");
            }
        }

        private static dboPersonStack GetCurrentdboPersonStack_new(int PersonID)
        {
            dboPersonStack result = new dboPersonStack();

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            using (var addressAMSvc = MdbService.GetDataService<dboPersonAdresseAM>())
            using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var mobileSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var faxSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>())
            using (var personDonationReceiptSvc = MdbService.GetDataService<dboPersonGruppe>())
            using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
            {
                result.person = personSvc.Read(new { PersonID = PersonID }).SingleOrDefault();

                var ids = personSvc.ExecuteQuery<IdRow>(Properties.Resources.ResourceManager.GetString("SqlTest1"), new { PersonID = PersonID }).SingleOrDefault();

                if (ids == null)
                {
                    Console.Write("No Data - ");
                }
                else
                {
                    if (ids.PersonAdresseID.HasValue)
                    {
                        result.address = addressSvc.Read(new { PersonAdresseID = ids.PersonAdresseID }).SingleOrDefault();
                        result.addressAM = addressAMSvc.Read(new { PersonAdresseID = ids.PersonAdresseID }).SingleOrDefault();
                    }

                    if (ids.Phone_PersonTelefonID.HasValue)
                        result.phone = phoneSvc.Read(new { PersonTelefonID = ids.Phone_PersonTelefonID }).SingleOrDefault();

                    if (ids.Mobile_PersonTelefonID.HasValue)
                        result.mobile = phoneSvc.Read(new { PersonTelefonID = ids.Mobile_PersonTelefonID }).SingleOrDefault();

                    if (ids.Fax_PersonTelefonID.HasValue)
                        result.fax = phoneSvc.Read(new { PersonTelefonID = ids.Fax_PersonTelefonID }).SingleOrDefault();

                    if (ids.PersonEmailID.HasValue)
                        result.email = emailSvc.Read(new { PersonEmailID = ids.PersonEmailID }).SingleOrDefault();

                    result.personDonationDeductionOptOut = personDonationDeductionOptOutSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 110493 }).FirstOrDefault();
                    result.personDonationReceipt = personDonationReceiptSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 20168 }).FirstOrDefault();

                    if (result.email != null)
                    {
                        result.emailNewsletter = emailNewsletterSvc.Read(new { PersonEmailID = result.email.PersonEmailID, zGruppeDetailID = 30104 }).FirstOrDefault();
                    }
                }
            }

            result.write_date = GetPersonWriteDate(result);
            result.sosync_write_date = GetPersonSosyncWriteDate(result);

            return result;
        }

        public class IdRow
        {
            public int? PersonAdresseID { get; set; }
            public int? Phone_PersonTelefonID { get; set; }
            public int? Mobile_PersonTelefonID { get; set; }
            public int? Fax_PersonTelefonID { get; set; }
            public int? PersonEmailID { get; set; }
        }

        private static dboPersonStack GetCurrentdboPersonStack(int PersonID)
        {
            Stopwatch s = new Stopwatch();
            s.Start();

            dboPersonStack result = new dboPersonStack();

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            using (var addressAMSvc = MdbService.GetDataService<dboPersonAdresseAM>())
            using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var mobileSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var faxSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>())
            using (var personDonationReceiptSvc = MdbService.GetDataService<dboPersonGruppe>())
            using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
            {
                result.person = personSvc.Read(new { PersonID = PersonID }).FirstOrDefault();

                result.address = (from iterAddress in addressSvc.Read(new { PersonID = PersonID })
                                  where iterAddress.GültigVon <= DateTime.Today &&
                                      iterAddress.GültigBis >= DateTime.Today
                                  orderby string.IsNullOrEmpty(iterAddress.GültigMonatArray) ? "111111111111" : iterAddress.GültigMonatArray descending,
                                  iterAddress.PersonAdresseID descending
                                  select iterAddress).FirstOrDefault();

                if (result.address != null)
                {
                    result.addressAM = addressAMSvc.Read(new { PersonAdresseID = result.address.PersonAdresseID }).FirstOrDefault();
                }

                result.phone = (from iterPhone in phoneSvc.Read(new { PersonID = PersonID })
                                where iterPhone.GültigVon <= DateTime.Today &&
                                    iterPhone.GültigBis >= DateTime.Today &&
                                    iterPhone.TelefontypID == 400
                                orderby string.IsNullOrEmpty(iterPhone.GültigMonatArray) ? "111111111111" : iterPhone.GültigMonatArray descending,
                                iterPhone.PersonTelefonID descending
                                select iterPhone).FirstOrDefault();

                result.mobile = (from itermobile in mobileSvc.Read(new { PersonID = PersonID })
                                 where itermobile.GültigVon <= DateTime.Today &&
                                     itermobile.GültigBis >= DateTime.Today &&
                                     itermobile.TelefontypID == 401
                                 orderby string.IsNullOrEmpty(itermobile.GültigMonatArray) ? "111111111111" : itermobile.GültigMonatArray descending,
                                 itermobile.PersonTelefonID descending
                                 select itermobile).FirstOrDefault();

                result.fax = (from iterfax in faxSvc.Read(new { PersonID = PersonID })
                              where iterfax.GültigVon <= DateTime.Today &&
                                  iterfax.GültigBis >= DateTime.Today &&
                                  iterfax.TelefontypID == 403
                              orderby string.IsNullOrEmpty(iterfax.GültigMonatArray) ? "111111111111" : iterfax.GültigMonatArray descending,
                              iterfax.PersonTelefonID descending
                              select iterfax).FirstOrDefault();

                result.email = (from iterEmail in emailSvc.Read(new { PersonID = PersonID })
                                where iterEmail.GültigVon <= DateTime.Today &&
                                    iterEmail.GültigBis >= DateTime.Today
                                orderby string.IsNullOrEmpty(iterEmail.GültigMonatArray) ? "111111111111" : iterEmail.GültigMonatArray descending,
                                iterEmail.PersonEmailID descending
                                select iterEmail).FirstOrDefault();

                result.personDonationDeductionOptOut = personDonationDeductionOptOutSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 110493 }).FirstOrDefault();

                result.personDonationReceipt = personDonationReceiptSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 20168 }).FirstOrDefault();


                if (result.email != null)
                {
                    result.emailNewsletter = emailNewsletterSvc.Read(new { PersonEmailID = result.email.PersonEmailID, zGruppeDetailID = 30104 }).FirstOrDefault();
                }
            }

            result.write_date = GetPersonWriteDate(result);
            result.sosync_write_date = GetPersonSosyncWriteDate(result);

            s.Stop();

            return result;
        }

        private static DateTime? GetPersonWriteDate(dboPersonStack person)
        {
            var query = new DateTime?[]
            {
                person.person != null ? person.person.write_date : (DateTime?)null,
                person.address != null ? person.address.write_date : (DateTime?)null,
                person.email != null ? person.email.write_date : (DateTime?)null,
                person.phone != null ? person.phone.write_date : (DateTime?)null,
                person.mobile != null ? person.mobile.write_date : (DateTime?)null,
                person.fax != null ? person.fax.write_date : (DateTime?)null,
                person.personDonationDeductionOptOut != null ? person.personDonationDeductionOptOut.write_date : (DateTime?)null,
                person.emailNewsletter != null ? person.emailNewsletter.write_date : (DateTime?)null,
                person.personDonationReceipt != null ? person.personDonationReceipt.write_date : (DateTime?)null
            }.Where(x => x.HasValue);

            if (query.Any())
                return query.Max();

            return null;
        }

        private static DateTime? GetPersonSosyncWriteDate(dboPersonStack person)
        {
            var query = new DateTime?[]
            {
                person.person != null ? person.person.sosync_write_date : (DateTime?)null,
                person.address != null ? person.address.sosync_write_date : (DateTime?)null,
                person.email != null ? person.email.sosync_write_date : (DateTime?)null,
                person.phone != null ? person.phone.sosync_write_date : (DateTime?)null,
                person.mobile != null ? person.mobile.sosync_write_date : (DateTime?)null,
                person.fax != null ? person.fax.sosync_write_date : (DateTime?)null,
                person.personDonationDeductionOptOut != null ? person.personDonationDeductionOptOut.sosync_write_date : (DateTime?)null,
                person.emailNewsletter != null ? person.emailNewsletter.sosync_write_date : (DateTime?)null,
                person.personDonationReceipt != null ? person.personDonationReceipt.sosync_write_date : (DateTime?)null
            }.Where(x => x.HasValue);

            if (query.Any())
                return query.Max();

            return null;
        }

    }

    public class Dummy
    {
        public string StringProp { get; set; }
    }
}