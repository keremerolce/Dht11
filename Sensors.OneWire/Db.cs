using SQLite.Net.Attributes;
using System;

namespace Sensors.OneWire
{
    public class Db
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public float Nem { get; set; }
        public float Sicaklik { get; set; }
        public DateTime Zaman { get; set; }
    }
}

