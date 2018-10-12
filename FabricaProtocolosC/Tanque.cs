using System;
using System.Collections.Generic;
using System.Text;

namespace POSstation.Protocolos
{
    public class Tanque
    {
        private int _ProductCode;
        public int ProductCode
        {
            get { return _ProductCode; }
            set { _ProductCode = value; }
        }

        private int _TankNumber;
        public int TankNumber
        {
            get { return _TankNumber; }
            set { _TankNumber = value; }
        }

        private bool _DeliveryInProgress;
        public bool DeliveryInProgress
        {
            get { return _DeliveryInProgress; }
            set { _DeliveryInProgress = value; }
        }

        private bool _LeakTestInProgress;
        public bool LeakTestInProgress
        {
            get { return _LeakTestInProgress; }
            set { _LeakTestInProgress = value; }
        }

        private bool _InvalidFuelHeighAlarm;
        public bool InvalidFuelHeighAlarm
        {
            get { return _InvalidFuelHeighAlarm; }
            set { _InvalidFuelHeighAlarm = value; }
        }

        private double _Volume;
        public double Volume
        {
            get { return _Volume; }
            set { _Volume = value; }
        }

        private double _TCVolume;
        public double TCVolume
        {
            get { return _TCVolume; }
            set { _TCVolume = value; }
        }

        private double _Ullage;
        public double Ullage
        {
            get { return _Ullage; }
            set { _Ullage = value; }
        }

        private double _Heigh;
        public double Heigh
        {
            get { return _Heigh; }
            set { _Heigh = value; }
        }

        private double _Water;
        public double Water
        {
            get { return _Water; }
            set { _Water = value; }
        }

        private double _Temperature;
        public double Temperature
        {
            get { return _Temperature; }
            set { _Temperature = value; }
        }

        private double _WaterVolume;
        public double WaterVolume
        {
            get { return _WaterVolume; }
            set { _WaterVolume = value; }
        }

        public int FactorVolumen { get; set; }

        public int FactorTCVolumen { get; set; }

        public int FactorUllage { get; set; }

        public int FactorHeight { get; set; }

        public int FactorHeightAgua { get; set; }

        public int FactorTemperatura { get; set; }

        public int FactorVolumenAgua { get; set; }
    }
}