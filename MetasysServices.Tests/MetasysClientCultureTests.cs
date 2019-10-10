using System;
using System.Net.Http;
using System.Linq;
using NUnit.Framework;
using JohnsonControls.Metasys.BasicServices;
using JohnsonControls.Metasys.BasicServices.Models;
using System.Globalization;

namespace Tests
{
    public class MetasysClientCultureTests
    {
        private const string Reliable = "reliabilityEnumSet.reliable";
        private const string PriorityNone = "writePriorityEnumSet.priorityNone";
        private const string Unsupported = "statusEnumSet.unsupportedObjectType";
        private const string Array = "dataTypeEnumSet.arrayDataType";
        MetasysClient client;

        [SetUp]
        public void Init()
        {
            client = new MetasysClient("hostname");
        }

        [TestCase("Reliable", "Unsupported object type", "0 (No Priority)", "Array")]
        public void TestCultureDefault(string reliable, string unsupported, string priority, string array)
        {
            Assert.AreEqual(reliable, client.Localize(Reliable));
            Assert.AreEqual(unsupported, client.Localize(Unsupported));
            Assert.AreEqual(priority, client.Localize(PriorityNone));
            Assert.AreEqual(array, client.Localize(Array));
        }

        [TestCase("en-US", "Reliable", "Unsupported object type", "0 (No Priority)", "Array")]
        [TestCase("cs-CZ", "Věrohodný", "Nepodporovaný typ objektu", "0 (Bez priority)", "Pole")]
        [TestCase("de-DE", "Zuverlässig", "Nicht-unterstützter Objekttyp", "0 (Keine Priorität)", "Anordnung")]
        [TestCase("es-ES", "Fiable", "Tipo de objeto no admitido", "0 (Sin prioridad)", "Matriz")]
        [TestCase("fr-FR", "Fiable", "Type d'objet non pris en charge", "0 (aucune priorité)", "Tableau")]
        [TestCase("hu-HU", "Megbízható", "Nem támogatott objektumtípus", "0 (Nincs prioritás)", "Tömb")]
        [TestCase("it-IT", "Affidabile", "Tipo di oggetto non supportato", "0 (Nessuna Priorità)", "Vettore")]
        [TestCase("ja-JP", "リライアブル", "対応しないオブジェクト タイプ", "0(優先順位なし)", "アレイ")]
        [TestCase("ko-KR", "냉방", "대응하지 않는 오브젝트 유형", "0 (우선순위 없슴)", "배열")]
        [TestCase("nb-NO", "Pålitelig", "Objekttype som ikke støttes", "0 (Ingen prioritet)", "Tabell")]
        [TestCase("nl-NL", "Betrouwbaar", "Niet-ondersteund objecttype", "0 (geen prioriteit)", "Matrix")]
        [TestCase("pl-PL", "Niezawodny", "Nieobsługiwany typ obiektu", "0 (Brak priorytetu)", "Tablica")]
        [TestCase("pt-BR", "Confiável", "Tipo de objeto não suportado", "0 (Sem Prioridade)", "Matriz")]
        [TestCase("ru-RU", "Надежный", "Неподдерживаемый тип объектов", "0 (приоритет отсутствует)", "Массив")]
        [TestCase("sv-SE", "Tillförlitlig", "Objekttypen stöds inte", "0 (Ingen prioritet)", "Uppsättning")]
        [TestCase("tr-TR", "Güvenilir", "Desteklenmeyen nesne türü", "0 (Öncelik Yok)", "Dizi")]
        [TestCase("zh-CN", "可靠", "不支持的对象类型", "0 (无优先级)", "数组")]
        [TestCase("zh-TW", "可靠", "不支持的对象类型", "0 (无优先级)", "数组")]
        public void TestCultures(string culture, string reliable, string unsupported, string priority, string array)
        {
            CultureInfo testCulture = new CultureInfo(culture);
            Assert.AreEqual(reliable, client.Localize(Reliable, testCulture));
            Assert.AreEqual(unsupported, client.Localize(Unsupported, testCulture));
            Assert.AreEqual(priority, client.Localize(PriorityNone, testCulture));
            Assert.AreEqual(array, client.Localize(Array, testCulture));
        }
    }
}