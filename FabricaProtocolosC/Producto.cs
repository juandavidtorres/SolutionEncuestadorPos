using System;
using System.Collections.Generic;
using System.Text;

namespace POSstation.Protocolos
{
    public class Producto
    {
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

        private int _IdProducto;
        public int IdProducto
        {
            get { return _IdProducto; }
            set { _IdProducto = value; }
        }
    }
}
