using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MySql.Data.MySqlClient;

namespace odev.Controllers
{
    public class HomeController : Controller
    {
        string connectionString = "Server=etrsoftodev.mysql.database.azure.com;Database=odev;Uid=superuser;Pwd=Bora2003!;";

        public ActionResult Cikti()
        {
            List<OdevViewModel> veriListesi = new List<OdevViewModel>();

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                string query = "SELECT * FROM tablo";
                MySqlCommand command = new MySqlCommand(query, connection);

                try
                {
                    connection.Open();
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            OdevViewModel row = new OdevViewModel
                            {
                                HesapKodu = reader["HesapKodu"].ToString(),
                                ToplamBorc = reader.GetDecimal("ToplamBorc")
                            };
                            veriListesi.Add(row);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Veri çekme hatası: {ex.Message}");
                }
            }

            var groupedData = veriListesi
                .GroupBy(x => GetFirstThreeDigits(x.HesapKodu))
                .Select(g => new GroupedData
                {
                    Kırılım1 = g.Key,
                    ToplamBorc = g.Sum(x => x.ToplamBorc),
                    Kırılım2Groups = g
                        .GroupBy(x => GetFirstFiveDigits(x.HesapKodu))
                        .Select(gg => new GroupedData
                        {
                            Kırılım2 = gg.Key,
                            ToplamBorc = gg.Where(x => x.HesapKodu != gg.Key).Sum(x => x.ToplamBorc), 
                            Kırılım3Groups = gg
                                .Select(x => new GroupedData
                                {
                                    Kırılım3 = x.HesapKodu,
                                    ToplamBorc = x.ToplamBorc
                                })
                                .Where(x => x.Kırılım3 != gg.Key) 
                                .ToList()
                        }).ToList()
                }).ToList();

            return View(groupedData);
        }

        private string GetFirstThreeDigits(string hesapKodu)
        {
            return hesapKodu.Length >= 3 ? hesapKodu.Substring(0, 3) : hesapKodu;
        }

        private string GetFirstFiveDigits(string hesapKodu)
        {
            return hesapKodu.Length >= 6 ? hesapKodu.Substring(0, 6) : hesapKodu;
        }
    }

    public class GroupedData
    {
        public string Kırılım1 { get; set; }
        public List<GroupedData> Kırılım2Groups { get; set; }
        public string Kırılım2 { get; set; }
        public List<GroupedData> Kırılım3Groups { get; set; }
        public string Kırılım3 { get; set; }
        public decimal ToplamBorc { get; set; }
    }

    public class OdevViewModel
    {
        public string HesapKodu { get; set; }
        public decimal ToplamBorc { get; set; }
    }
}
