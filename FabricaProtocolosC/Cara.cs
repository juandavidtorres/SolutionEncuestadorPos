using System;
using System.Collections.Generic;
using System.Text;

namespace POSstation.Protocolos
{
    public class Cara
    {
        private Int16 _IdCara;
        private Boolean _Activa;
        private Int16 _IdSurtidor;
        private Int32 _FactorPrecio;
        private Int32 _FactorVolumen;
        private Int32 _FactorImporte;
        private Int32 _FactorTotalizador;
        private Boolean _EsVentaParcial;
        //private bool _EsVentaGerenciada;
        private Decimal _LecturaInicialVParcial;
        private Boolean _AplicaControlPresionLLenado;


        public Cara()
        {
        }

        public Int16 IdCara
        {
            get { return _IdCara; }
            set { _IdCara = value; }
        }

        public Int16 IdSurtidor
        {
            get { return _IdSurtidor; }
            set { _IdSurtidor = value; }
        }

        public Int32 FactorPrecio
        {
            get { return _FactorPrecio; }
            set { _FactorPrecio = value; }
        }

        public Int32 FactorVolumen
        {
            get { return _FactorVolumen; }
            set { _FactorVolumen = value; }
        }

        public Int32 FactorImporte
        {
            get { return _FactorImporte; }
            set { _FactorImporte = value; }
        }

        public Int32 FactorTotalizador
        {
            get { return _FactorTotalizador; }
            set { _FactorTotalizador = value; }
        }

        public Boolean Activa
        {
            get { return _Activa; }
            set { _Activa = value; }
        }

        public Boolean EsVentaParcial
        {
            get { return _EsVentaParcial; }
            set { _EsVentaParcial = value; }
        }

        public Decimal LecturaInicialVParcial
        {
            get
            {
                return _LecturaInicialVParcial;
            }
            set
            {
                _LecturaInicialVParcial = value;
            }
        }

        public Boolean AplicaControlPresionLLenado
        {
            get { return _AplicaControlPresionLLenado; }
            set { _AplicaControlPresionLLenado = value; }
        }

    }
}