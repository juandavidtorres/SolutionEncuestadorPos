using System;
using System.Collections.Generic;
using System.Text;

//Ultima Modificación: 23.10.2009-18:24
namespace POSstation.Protocolos
{
    public class Grados
    {
        private int _IdProducto;
        public int IdProducto
        {
            get { return _IdProducto; }
            set { _IdProducto = value; }
        }

        private decimal _PrecioNivel1;
        public decimal PrecioNivel1
        {
            get { return _PrecioNivel1; }
            set { _PrecioNivel1 = value; }
        }

        private decimal _PrecioNivel2;
        public decimal PrecioNivel2
        {
            get { return _PrecioNivel2; }
            set { _PrecioNivel2 = value; }
        }

        private decimal _PrecioSurtidorNivel1;
        public decimal PrecioSurtidorNivel1
        {
            get { return _PrecioSurtidorNivel1; }
            set { _PrecioSurtidorNivel1 = value; }
        }

        private decimal _PrecioSurtidorNivel2;
        public decimal PrecioSurtidorNivel2
        {
            get { return _PrecioSurtidorNivel2; }
            set { _PrecioSurtidorNivel2 = value; }
        }

        private bool _CambioPrecio;
        public bool CambioPrecio
        {
            get { return _CambioPrecio; }
            set { _CambioPrecio = value; }
        }

        private int _MangueraBD;
        public int MangueraBD
        {
            get { return _MangueraBD; }
            set { _MangueraBD = value; }
        }

        private byte _NoGrado;
        public byte NoGrado
        {
            get { return _NoGrado; }
            set { _NoGrado = value; }
        }

        private decimal _Lectura;
        public decimal Lectura
        {
            get { return _Lectura; }
            set { _Lectura = value; }
        }


        private decimal _LecturaVenta; //Recupera la lectura de volumen Totalizador
        public decimal LecturaVenta
        {
            get { return _LecturaVenta; }
            set { _LecturaVenta = value; }
        }

        
        decimal _LecturaInicialVenta;            //Almacena la LECTURA INICIAL de cada venta en curso
        public decimal LecturaInicialVenta
        {
            get { return _LecturaInicialVenta; }
            set { _LecturaInicialVenta = value; }
        }

        decimal _LecturaFinalVenta;              //Almacena la LECTURA FINAL de cada venta en curso
        public decimal LecturaFinalVenta
        {
            get { return _LecturaFinalVenta; }
            set { _LecturaFinalVenta = value; }
        }
        

        private decimal _LecturaImporte; //Importe para wayne duplex
        public decimal LecturaImporte
        {
            get { return  _LecturaImporte;}
            set{_LecturaImporte= value;}
        }

        decimal _LecturaInicialImporte;            //Almacena la LECTURA INICIAL de cada venta en curso importe
        public decimal LecturaInicialImporte
        {
            get { return _LecturaInicialImporte; }
            set { _LecturaInicialImporte = value; }
        }


        decimal _LecturaFinalImporte;              //Almacena la LECTURA FINAL de cada venta en curso importe
        public decimal LecturaFinalImporte
        {
            get { return _LecturaFinalImporte; }
            set { _LecturaFinalImporte = value; }
        }


        //Importe totalizadores: 2A
        // **************************************
        //Almacena la el Totalizador de importe actual
        decimal _TotalizadorImporte;              
        public decimal TotalizadorImporte
        {
            get { return _TotalizadorImporte; }
            set { _TotalizadorImporte = value; }
        }


        //Almacena la el Totalizador de importe Inicial
        decimal _TotalizadorImporte_Inicial;
        public decimal TotalizadorImporte_Inicial
        {
            get { return _TotalizadorImporte_Inicial; }
            set { _TotalizadorImporte_Inicial = value; }
        }

        //Almacena la el Totalizador de importe Final
        decimal _TotalizadorImporte_Final;             
        public decimal TotalizadorImporte_Final
        {
            get { return _TotalizadorImporte_Final; }
            set { _TotalizadorImporte_Final = value; }
        }

    




        //Volumen Totalizador: 2A
        // **************************************
        //Almacena la el Totalizador de Volumen Actual
        decimal _TotalizadorVolumen;              
        public decimal TotalizadorVolumen
        {
            get { return _TotalizadorVolumen; }
            set { _TotalizadorVolumen = value; }
        }

        //Almacena la el Totalizador de Volumen Inicial
        decimal _TotalizadorVolumen_Inicial;
        public decimal TotalizadorVolumen_Inicial
        {
            get { return _TotalizadorVolumen_Inicial; }
            set { _TotalizadorVolumen_Inicial = value; }
        }

        //Almacena la el Totalizador de Volumen Final
        decimal _TotalizadorVolumen_Final;
        public decimal TotalizadorVolumen_Final
        {
            get { return _TotalizadorVolumen_Final; }
            set { _TotalizadorVolumen_Final = value; }
        }





        // **************************************

        private decimal _PresionLlenado;
        public decimal PresionLlenado
        {
            get { return _PresionLlenado; }
            set { _PresionLlenado = value; }
        }

        private int _IdProductoACambiar;
        public int IdProductoACambiar
        {
            get
            {
                return _IdProductoACambiar;
            }
            set
            {
                _IdProductoACambiar = value;
            }
        }

        private bool _Autorizar;
        public bool Autorizar
        {
            get
            {
                return _Autorizar;
            }
            set
            {
                _Autorizar = value;
            }
        }

        private bool _CambiarProducto;
        public bool CambiarProducto
        {
            get
            {
                return _CambiarProducto;
            }
            set
            {
                _CambiarProducto = value;
            }
        }

        private bool _CambioPrecioVentaActivo;
        public bool CambioPrecioVentaActivo
        {
            get { return _CambioPrecioVentaActivo; }
            set { _CambioPrecioVentaActivo = value; }
        }

        decimal _Volumen_Venta_Anterior;              //Almacena la LECTURA FINAL de cada venta en curso
        public decimal Volumen_Venta_Anterior
        {
            get { return _Volumen_Venta_Anterior; }
            set { _Volumen_Venta_Anterior = value; }
        }
        
    }
}
