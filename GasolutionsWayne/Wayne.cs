

using System;
using System.Collections.Generic;
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Text;
using System.Threading;         //Para manejo del Timer
using System.Timers;            //Para manejo del Timer
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;

//namespace POSstation.Protocolos.FabricaProtocolosC.Protocolos.Wayne
namespace POSstation.Protocolos
{
    public class Wayne : iProtocolo
    {

        #region EventoDeProtocolo

        private bool AplicaWindows = false;
        private bool AplicaTramas = true;
        public event iProtocolo.CambioMangueraEnVentaGerenciadaEventHandler CambioMangueraEnVentaGerenciada;//

        public event iProtocolo.CaraEnReposoEventHandler CaraEnReposo;//--Listo

        public event iProtocolo.VentaFinalizadaEventHandler VentaFinalizada;//Listo G

        public event iProtocolo.LecturaTurnoCerradoEventHandler LecturaTurnoCerrado;//Listo G

        public event iProtocolo.LecturaTurnoAbiertoEventHandler LecturaTurnoAbierto;//--Listo G

        public event iProtocolo.LecturaInicialVentaEventHandler LecturaInicialVenta;//--Listo G

        public event iProtocolo.VentaParcialEventHandler VentaParcial;//Listo G

        public event iProtocolo.CambioPrecioFallidoEventHandler CambioPrecioFallido;//Listo G

        public event iProtocolo.CancelarProcesarTurnoEventHandler CancelarProcesarTurno;//--Listo

        public event iProtocolo.ExcepcionOcurridaEventHandler ExcepcionOcurrida;//--

        public event iProtocolo.VentaInterrumpidaEnCeroEventHandler VentaInterrumpidaEnCero;//Listo G

        public event iProtocolo.AutorizacionRequeridaEventHandler AutorizacionRequerida;//--Listo G

        public event iProtocolo.IniciarCambioTarjetaEventHandler IniciarCambioTarjeta;//

        public event iProtocolo.LecturasCambioTarjetaEventHandler LecturasCambioTarjeta;//

        public event iProtocolo.NotificarCambioPrecioMangueraEventHandler NotificarCambioPrecioManguera;//Listo G

        #endregion



        #region DECLARACION DE VARIABLES Y DEFINICIONES

        Dictionary<byte, RedSurtidor> PropiedadesCara;        //Diccionario donde se almacenan las Caras y sus propiedades

        public enum ComandoSurtidor
        {

            //Mensajes de Pedido de Informacion
            Estado,
            Autorizar,
            ObtenerPrecio,
            ObtenerVentaDinero,
            ObtenerVentaVolumen,
            EstalecerPrecio,
            Predeterminar,
            ObtenerTotalizador_I,
            ObtenerTotalizador_II,
            Preset_Volumen,
        }   //Define los COMANDOS que se envian al Surtidor

        ComandoSurtidor ComandoCaras;

        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
        byte GradoEncuesta;             // Grado o Manguera a Encuestar
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO
        bool TramaEco;                  //Bandera que indica si dentro de la trama respuesta viene eco o no
        string AuxiliarLectura;

        /*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        bool FalloComunicacion;      //Almacena el tipo de fallo de comunicacion        

        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR       

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        //Controla la comunicacion entre las aplicaciones por medio de eventos

        //VARIABLES UTILZADAS PARA LOGUEO TXT

        //Variable que almacen la ruta y el nombre del archivo que guarda inconsistencias en el proceso logico
        string Archivo;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWRegistro;
        //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        string ArchivoTramas;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWTramas;
        bool CondicionCiclo = true;
        byte CaraID;//DCF Alias
        byte CaraTmp; // Utilizado para las caras con alias mas de 16 caras

        decimal difer; //diferencia en Lf- Li



        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        string Puerto;

        AsyncCallback callBack = new AsyncCallback(CallBackMethod);
        TcpClient ClienteWayne;
        NetworkStream Stream;
        int Bytes_leidos;
        bool LogueoTramas = false;


        bool CondicionCiclo2 = true;
        bool EncuentaFinalizada = false;

        string strPresetVol;
        byte strPresetVolA;
        byte strPresetVolM;
        byte strPresetVolB;

        #endregion

        #region PUNTO DE ARRANQUE
        //PUNTO DE ARRANQUE DE LA CLASE
        public Wayne(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                //DCF 20/03/2018 Logueo Tramas
                Generic.Settings Config = new Generic.Settings(); //DCF 06-09-2017
                LogueoTramas = Boolean.Parse(Config.GetValue("Logueo"));  //DCF 06-09-2017

                this.AplicaWindows = true;

                this.Puerto = Puerto; //DCF Archivos .txt 08/03/2018  

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "-Wayne-Tramas(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "- Wayne-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.05.13-1613");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.08.09-1100"); //Alias DCF //Dimensión de Archivos de logueo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.11.29-2000"); //Precio Peru
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.01.18-1403"); //tiempos
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.02.11-1115"); //Cambio de producto y precio
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.04.29-1530"); //MultiplicadorPrecioVenta para terpel 5 digitos
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.06.03-1134"); //No Autorizar con manguera colgada 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.07.27-0945"); //Se asegura obtener siempre la lectura Inicial de volumen al finalizar la venta, para corregir el error de lecturas, en caso que el grado autorizado no sea el que despacho //DCF 27-07-2011 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.07.29-1625"); //DCF 29-07-2011 EsVentaParcial = false; indica que no se realizó venta 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.21-1630"); //Venta en Cero si LF = LI, indicado Por wal                
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.22-1240"); //Venta en Cero si LF = LI, indicado Por wal                
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.22-1416"); //  case EstadoCara.WayneFinDespacho: //NEW en gas norte
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.25-0905"); //  Actualización Grado que vendió y no envió de Autorización en estado Preseteada 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.04.10-1806"); //   //Porcentaje del 1% //DCF 10-04-2012 corrección para Perú  ventas 9,999 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.04.23-0901");  // -- Modificado 2012.04.23-0901 -- Log
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.04.26-1522");   //corregir las pérdidas de venta por el estado (0F) WaynePredeterminada con manguera levantada
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.06.21-1623");   //Alias IDcara
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.06.29-1625");   //29/06/2012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.07.30-0840");   // se Incrementa al 2% TotalVentaCalculada 30/07/2012 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2013.01.24-1730"); //  Se actualizó para agregar los constructores del servicio de terpel
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2013.10.31-7119. "); //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2016.11.01-1631"); //TCP/ Se cambia el VerifySizeFile()
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2016.11.24-1550"); //24-11-2016 DCF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.06.15-827"); //15-06-2017 DCF //se carga el grado en que se hizo la venta despues del reinicio del sistema
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.10.03-1826_TCP *");//DCF 25/09/2017
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.11.29-1451_TCP *");//DCF 29/11/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.12.18- 1118_TCP  *");//PuertoCom.BaudRate = 4800; Cali Barrio Nuevo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.03.08- 1855_TCP  *");//DCF Archivos .txt 08/03/2018 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.03.20- 1519_TCP *");///DCF 20/03/2018 Logueo Tramas
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.06.26- 1827_TCP ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.06.27- 1013_TCP ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.07.06- 1604_TCP ");//DCF 06/07/2018
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.08.14- 1134_TCP ");// DCF 14/08/2018 para programar aa full cuando el cliente tenga valores superiores al millon 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.08.28- 2317_TCP *");// DCF 28/08/2018 Preset por Volumen Cartago  
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.09.10- 1332_TCP *");//DCF 10/09/2018 FactorPredeterminacionVolumen no esta en PropiedadesCara

                SWRegistro.Flush();
                //Instancia los eventos disparados por la aplicacion cliente


                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Odd;
                    PuertoCom.Open();
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }

                //PropiedadesCara es la referencia con la que se va a trabajar
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                foreach (RedSurtidor oCara in PropiedadesCara.Values)
                {
                    foreach (Grados oGrado in PropiedadesCara[oCara.Cara].ListaGrados)
                    {

                        if (PropiedadesCara[oCara.Cara].EsVentaParcial)//DCF 25/09/2017
                        {
                            if (Convert.ToInt32(PropiedadesCara[oCara.Cara].GradoMangueraVentaParcial) >= 0)
                            {
                                PropiedadesCara[oCara.Cara].GradoVenta = Convert.ToInt32(oCara.GradoMangueraVentaParcial);//15-06-2017 DCF
                                SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                                    " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " - Venta Parcial: " + oCara.EsVentaParcial + " -GradoMangueraVentaParcial: " + oCara.GradoMangueraVentaParcial + " -LogueoTramas = " + LogueoTramas);
                            }
                        }
                    }
                }


                SWRegistro.Flush();

                //Variable que determina si la interfaz física de los surtidores añade eco a las tramas recibida
                TramaEco = Eco;


                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Eco = " + Eco);//DCF 29/11/2017
                SWRegistro.Flush();        

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                HiloCicloCaras.Start();
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + PropiedadesCara.Count + "|Excepcion|Wayne: " + Excepcion);
                SWRegistro.Flush();
            }
        }
    

        public Wayne(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                //DCF 20/03/2018 Logueo Tramas
                Generic.Settings Config = new Generic.Settings(); //DCF 06-09-2017
                LogueoTramas = Boolean.Parse(Config.GetValue("Logueo"));  //DCF 06-09-2017

                this.AplicaWindows = true;
                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "-Wayne-Tramas(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "- Wayne-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);





                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Puerto;
           
                TramaEco = Eco;

                if (EsTCPIP)
                {
                    try
                    {
                        //Crea y abre la conexión con el Servidor
                        ClienteWayne = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClienteWayne.GetStream();

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }


                }


                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.05.13-1613");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.08.09-1100"); //Alias DCF //Dimensión de Archivos de logueo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.11.29-2000"); //Precio Peru
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.01.18-1403"); //tiempos
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.02.11-1115"); //Cambio de producto y precio
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.04.29-1530"); //MultiplicadorPrecioVenta para terpel 5 digitos
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.06.03-1134"); //No Autorizar con manguera colgada 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.07.27-0945"); //Se asegura obtener siempre la lectura Inicial de volumen al finalizar la venta, para corregir el error de lecturas, en caso que el grado autorizado no sea el que despacho //DCF 27-07-2011 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.07.29-1625"); //DCF 29-07-2011 EsVentaParcial = false; indica que no se realizó venta 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.21-1630"); //Venta en Cero si LF = LI, indicado Por wal                
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.22-1240"); //Venta en Cero si LF = LI, indicado Por wal                
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.22-1416"); //  case EstadoCara.WayneFinDespacho: //NEW en gas norte
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.10.25-0905"); //  Actualización Grado que vendió y no envió de Autorización en estado Preseteada 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.04.10-1806"); //   //Porcentaje del 1% //DCF 10-04-2012 corrección para Perú  ventas 9,999 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.04.23-0901");  // -- Modificado 2012.04.23-0901 -- Log
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.04.26-1522");   //corregir las pérdidas de venta por el estado (0F) WaynePredeterminada con manguera levantada
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.06.21-1623");   //Alias IDcara
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.06.29-1625");   //29/06/2012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2012.07.30-0840");   // se Incrementa al 2% TotalVentaCalculada 30/07/2012 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2013.01.24-1730"); //  Se actualizó para agregar los constructores del servicio de terpel
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2013.10.31-7119"); //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2014.09.02-0942"); //TCP/IP
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2015.08.05-0942"); //TCP/IP-WBeleno
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2016.11.01-1631"); //TCP/ Se cambia el VerifySizeFile()
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2016.11.24-1550"); //24-11-2016 DCF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.06.15-827_TCP"); //15-06-2017 DCF //se carga el grado en que se hizo la venta despues del reinicio del sistema
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.10.03-1826_TCP");//DCF 25/09/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.11.29-1451_TCP ");//DCF 29/11/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.12.18- 1118_TCP ");//PuertoCom.BaudRate = 4800; Cali Barrio Nuevo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.03.20- 1519_TCP ");///DCF 20/03/2018 Logueo Tramas
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.06.26- 1827_TCP ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.06.27- 1013_TCP ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.07.06- 1604_TCP ");//DCF 06/07/2018
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.08.14- 1134_TCP *");// DCF 14/08/2018 para programar aa full cuando el cliente tenga valores superiores al millon 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.08.28- 2317_TCP *");// DCF 28/08/2018 Preset por Volumen Cartago 
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.09.10- 1332_TCP *");//DCF 10/09/2018 FactorPredeterminacionVolumen no esta en PropiedadesCara
                SWRegistro.Flush();
                //Instancia los eventos disparados por la aplicacion cliente


               
                //PropiedadesCara es la referencia con la que se va a trabajar
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                foreach (RedSurtidor oCara in PropiedadesCara.Values)
                {
                    foreach (Grados oGrado in PropiedadesCara[oCara.Cara].ListaGrados)
                    {

                        if (PropiedadesCara[oCara.Cara].EsVentaParcial)//DCF 25/09/2017
                        {
                            if (Convert.ToInt32(PropiedadesCara[oCara.Cara].GradoMangueraVentaParcial) >= 0)
                            {
                                PropiedadesCara[oCara.Cara].GradoVenta = Convert.ToInt32(oCara.GradoMangueraVentaParcial);//15-06-2017 DCF
                                SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                                    " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " - Venta Parcial: " + oCara.EsVentaParcial + " -GradoMangueraVentaParcial: " + oCara.GradoMangueraVentaParcial + " -LogueoTramas = " + LogueoTramas);
                            }
                        }
                        
                    }
                }
                SWRegistro.Flush();

                //Variable que determina si la interfaz física de los surtidores añade eco a las tramas recibida
                TramaEco = Eco;


                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Eco = " + Eco);//DCF 29/11/2017
                SWRegistro.Flush();        

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                HiloCicloCaras.Start();
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + PropiedadesCara.Count + "|Excepcion|Wayne: " + Excepcion);
                SWRegistro.Flush();
            }
        }



        private byte ConvertirCaraBD(byte caraBD) //YEZID Alias de las caras //DCF 2011-05-14
        {
            byte CaraSurtidor = 0;
            try
            {
                foreach (RedSurtidor ORedCaras in PropiedadesCara.Values)
                {
                    if (ORedCaras.CaraBD == caraBD)
                    {
                        CaraSurtidor = ORedCaras.Cara;
                        break;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en funcion ConvertirCaraBD";
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();

            }
            return CaraSurtidor;
        }




        //CICLO INFINITO DE RECORRIDO DE LAS CARAS (REEMPLAZO DEL TIMER)
        private void CicloCara()
        {
            try
            {
                //Variable para garantizar el ciclo infinito
                CondicionCiclo = true;

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Inicia ciclo de encuesta a " + PropiedadesCara.Count + " caras");
                SWRegistro.Flush();


                foreach (RedSurtidor ORedCaras2 in PropiedadesCara.Values)
                {
                    byte CaraEncuestada2 = ORedCaras2.Cara;

                    //if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)-- 29/06/2012
                    if (PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta == 0)
                    {
                        PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta = 1;
                    }

                    if (PropiedadesCara[CaraEncuestada2].FactorPredeterminacionVolumen == 0)// DCF 28/08/2018 Preset por Volumen Cartago  
                    {
                        PropiedadesCara[CaraEncuestada2].FactorPredeterminacionVolumen = 1000;
                    }



                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada2 + "|FactorVolumen: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorVolumen)
                           + " - FactorTotalizador: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorTotalizador)
                           + " - FactorImporte: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorImporte)
                           + " - FactorPrecio: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorPrecio)
                           + " - MultiplicadorPrecioVenta: " + PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta
                           + " - FactorPredeterminacionVolumen: " + PropiedadesCara[CaraEncuestada2].FactorPredeterminacionVolumen); //DCF 10/09/2018 FactorPredeterminacionVolumen no esta en PropiedadesCara
                    SWRegistro.Flush();


                   

                }






                //Ciclo Infinito
                while (CondicionCiclo)
                {
                    VerifySizeFile();
                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in PropiedadesCara.Values)
                    {

                        if (CondicionCiclo2)
                        {
                            //Si la cara está activa, realizar proceso de encuesta
                            if (ORedCaras.Activa == true)
                            {
                                CaraEncuestada = ORedCaras.Cara;

                                CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                                EncuentaFinalizada = false;

                                if (ProcesoEnvioComando(ComandoSurtidor.Estado)) //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                    TomarAccion();

                                EncuentaFinalizada = true;

                            }
                            Thread.Sleep(20);
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {

                EncuentaFinalizada = true;
                CondicionCiclo2 = true;

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Wayne: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        #endregion

        public void VerifySizeFile()
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas); //TCP/ Se cambia el VerifySizeFile()

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "-Wayne-Tramas(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }


                //FileInfo 
                FileInf = new FileInfo(Archivo);//TCP/ Se cambia el VerifySizeFile()
                if (FileInf.Length > 30000001)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "- Wayne-Sucesos(" + Puerto + ").txt";
                    SWRegistro = File.AppendText(Archivo);
                }


            }
            catch (Exception ex)
            {
                try
                {
                    string MensajeExcepcion = "Mensaje en VerifySizeFile: " + ex;
                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Warning|" + MensajeExcepcion);//TCP/ Se cambia el VerifySizeFile()
                    SWRegistro.Flush();
                }
                catch (Exception)
                {

                }
            }

        }

        #region CONSTRUCCIÓN, ENVÍO Y RECEPCIÓN DE TRAMAS DE COMUNICACIÓN

        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        private bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                ComandoCaras = ComandoaEnviar;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 4;// antes 2 DCF

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

             

                do
                {
                    //Arma la trama de Transmision
                    ArmarTramaTx();
                   
                    if (EsTCPIP)
                        EnviarComando_TCPIP();
                    else
                        EnviarComando();

                    //Analiza la información recibida si se espera respuesta del Surtidor
                    if (EsTCPIP)
                        RecibirInformacion_TCPIP();
                    else
                     RecibirInformacion();

                    Reintentos += 1;
                } while (FalloComunicacion == true && Reintentos < MaximoReintento);


                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion)
                {
                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {
                        string MensajeErrorLectura = "Error en Comunicacion con Surtidor";
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            bool EstadoTurno = false;
                            PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                            if (AplicaWindows)
                            {
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                            }

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                            if (AplicaWindows)
                            {
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                            }

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                    }

                    if (!PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Perdida de comunicacion en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].FalloReportado = true;
                    }

                    //Regresa el parámetro FALSE si hubo error en la trama o en la comunicación con el surtidor
                    return false;
                }
                else
                {
                    if (PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Se reestablece comunciación con surtidor en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoEnviocomando: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        private void ArmarTramaTx()
        {
            try
            {
                byte CaraWayne;
                switch (ComandoCaras)
                {
                    case ComandoSurtidor.Estado:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseI();
                        TramaTx = new byte[5] { 0x00, 0x00, CaraWayne, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.Autorizar:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x8F, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.ObtenerPrecio:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x00, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;


                    case ComandoSurtidor.EstalecerPrecio:
                        TimeOut = 300;
                        CaraWayne = AsignacionCaraClaseIII();

                        string strPrecioHex = Convert.ToInt32(
                            (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1
                            * PropiedadesCara[CaraEncuestada].FactorPrecio) / PropiedadesCara[CaraEncuestada].MultiplicadorPrecioVenta).ToString("X2").PadLeft(4, '0');

                        ////DCF // no se generra erro por el factor del precio multiplicacion 
                        //string strPrecioHex = Convert.ToInt32(
                        //    (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1)).ToString("X2").PadLeft(4, '0');

                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|PrecioH: " +
                        //    Convert.ToByte(strPrecioHex.Substring(strPrecioHex.Length - 4, 2), 16).ToString("X2") +
                        //    " - PrecioL: " + Convert.ToByte(strPrecioHex.Substring(strPrecioHex.Length - 2, 2), 16).ToString("X2") + " strPrecioHex =" + strPrecioHex);
                        //SWRegistro.Flush();

                        byte PrecioH = Convert.ToByte(strPrecioHex.Substring(strPrecioHex.Length - 4, 2), 16);
                        byte PrecioL = Convert.ToByte(strPrecioHex.Substring(strPrecioHex.Length - 2, 2), 16);
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x01, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara), 0x00, PrecioL, 0x00, PrecioH, 0x00, 0xFF };
                        break;


                    case ComandoSurtidor.ObtenerVentaDinero:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.ObtenerVentaVolumen:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x26, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.ObtenerTotalizador_I:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x02, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara + 0x30), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.ObtenerTotalizador_II:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x04, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara + 0x30), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.Predeterminar:
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();

                        if (PropiedadesCara[CaraEncuestada].ValorPredeterminado > 999999) // DCF 14/08/2018 para programar aa full cuando el cliente tenga valores superiores al millon 
                        {
                            // PropiedadesCara[CaraEncuestada].ValorPredeterminado = 999999;  convertir el valor a volumne 


                            // DCF 28/08/2018 Preset por Volumen Cartago  

                            decimal precio_VOL = ((PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 * PropiedadesCara[CaraEncuestada].FactorPrecio) 
                                                   / PropiedadesCara[CaraEncuestada].MultiplicadorPrecioVenta);
                               

                            strPresetVol =
                               Convert.ToInt64((PropiedadesCara[CaraEncuestada].ValorPredeterminado / precio_VOL) * PropiedadesCara[CaraEncuestada].FactorPredeterminacionVolumen ).ToString().PadLeft(6, '0');
                             strPresetVolA = Convert.ToByte(strPresetVol.Substring(strPresetVol.Length - 6, 2), 16);
                             strPresetVolM = Convert.ToByte(strPresetVol.Substring(strPresetVol.Length - 4, 2), 16);
                             strPresetVolB = Convert.ToByte(strPresetVol.Substring(strPresetVol.Length - 2, 2), 16);
                            TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x23, 0x00, strPresetVolB, 0x00, strPresetVolM, 0x00, strPresetVolA, 0x00, 0xFF };


                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Predeterminacion Convertida a Volumen: " + strPresetVol);
                            SWRegistro.Flush();
                       
                        }
                        else
                        {

                            string strPreset =
                                Convert.ToInt64(PropiedadesCara[CaraEncuestada].ValorPredeterminado * PropiedadesCara[CaraEncuestada].FactorImporte).ToString().PadLeft(6, '0');
                            byte PresetA = Convert.ToByte(strPreset.Substring(strPreset.Length - 6, 2), 16);
                            byte PresetM = Convert.ToByte(strPreset.Substring(strPreset.Length - 4, 2), 16);
                            byte PresetB = Convert.ToByte(strPreset.Substring(strPreset.Length - 2, 2), 16);
                            TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x21, 0x00, PresetB, 0x00, PresetM, 0x00, PresetA, 0x00, 0xFF };//Preset por dinero 

                        }

                        break;


                    case ComandoSurtidor.Preset_Volumen: // DCF 28/08/2018 Preset por Volumen Cartago  
                 
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();                    

                        strPresetVol =
                            Convert.ToInt64(PropiedadesCara[CaraEncuestada].ValorPredeterminado * PropiedadesCara[CaraEncuestada].FactorPredeterminacionVolumen).ToString().PadLeft(6, '0');
                         strPresetVolA = Convert.ToByte(strPresetVol.Substring(strPresetVol.Length - 6, 2), 16);
                         strPresetVolM = Convert.ToByte(strPresetVol.Substring(strPresetVol.Length - 4, 2), 16);
                         strPresetVolB = Convert.ToByte(strPresetVol.Substring(strPresetVol.Length - 2, 2), 16);
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x23, 0x00, strPresetVolB, 0x00, strPresetVolM, 0x00, strPresetVolA, 0x00, 0xFF };
                        break;
                }

                //Calcula y almacena en Trama el complemento 256 de cada Byte 
                for (int i = 3; i < TramaTx.Length - 1; i += 2)
                    TramaTx[i] = Convert.ToByte(0xFF - TramaTx[i - 1]);

                //WBeleno: 2015.08.05
                /////////////////////////////////////////////////////////////////////
                //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
                if (TramaEco)
                    eco = Convert.ToByte(TramaTx.Length); //respuesta del LOOP de Corriente
                else
                    eco = 0;
                /////////////////////////////////////////////////////////////////////

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ArmarTramaTx: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        private byte AsignacionCaraClaseI() //Codigo de cara Encuestada para Comando ESTADO 
        {
            try
            {
                byte CaraWayne = new byte();
                switch (CaraEncuestada)
                {
                    case 1:
                        CaraWayne = 0x09;
                        break;
                    case 2:
                        CaraWayne = 0x11;
                        break;
                    case 3:
                        CaraWayne = 0x19;
                        break;
                    case 4:
                        CaraWayne = 0x21;
                        break;
                    case 5:
                        CaraWayne = 0x29;
                        break;
                    case 6:
                        CaraWayne = 0x31;
                        break;
                    case 7:
                        CaraWayne = 0x39;
                        break;
                    case 8:
                        CaraWayne = 0x41;
                        break;
                    case 9:
                        CaraWayne = 0x49;
                        break;
                    case 10:
                        CaraWayne = 0x51;
                        break;
                    case 11:
                        CaraWayne = 0x59;
                        break;
                    case 12:
                        CaraWayne = 0x61;
                        break;
                    case 13:
                        CaraWayne = 0x69;
                        break;
                    case 14:
                        CaraWayne = 0x71;
                        break;
                    case 15:
                        CaraWayne = 0x79;
                        break;
                    case 16:
                        CaraWayne = 0x81;
                        break;
                }
                return CaraWayne;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|AsignacionCaraClaseI: " + Excepcion);
                SWRegistro.Flush();
                return 0;
            }
        }
        private byte AsignacionCaraClaseII()//Codigo de cara Encuestada para Comando AUTORIZAR 
        {
            try
            {
                byte CaraWayne = new byte();
                switch (CaraEncuestada)
                {
                    case 1:
                        CaraWayne = 0x08;
                        break;
                    case 2:
                        CaraWayne = 0x10;
                        break;
                    case 3:
                        CaraWayne = 0x18;
                        break;
                    case 4:
                        CaraWayne = 0x20;
                        break;
                    case 5:
                        CaraWayne = 0x28;
                        break;
                    case 6:
                        CaraWayne = 0x30;
                        break;
                    case 7:
                        CaraWayne = 0x38;
                        break;
                    case 8:
                        CaraWayne = 0x40;
                        break;
                    case 9:
                        CaraWayne = 0x48;
                        break;
                    case 10:
                        CaraWayne = 0x50;
                        break;
                    case 11:
                        CaraWayne = 0x58;
                        break;
                    case 12:
                        CaraWayne = 0x60;
                        break;
                    case 13:
                        CaraWayne = 0x68;
                        break;
                    case 14:
                        CaraWayne = 0x70;
                        break;
                    case 15:
                        CaraWayne = 0x78;
                        break;
                    case 16:
                        CaraWayne = 0x80;
                        break;
                }
                return CaraWayne;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|AsignacionCaraClaseII: " + Excepcion);
                SWRegistro.Flush();
                return 0;
            }
        }
        private byte AsignacionCaraClaseIII()//Codigo de cara Encuestada para Comando PRECIO DE VENTA 
        {
            try
            {
                byte CaraWayne = new byte();
                switch (CaraEncuestada)
                {
                    case 1:
                        CaraWayne = 0x0F;
                        break;
                    case 2:
                        CaraWayne = 0x17;
                        break;
                    case 3:
                        CaraWayne = 0x1F;
                        break;
                    case 4:
                        CaraWayne = 0x27;
                        break;
                    case 5:
                        CaraWayne = 0x2F;
                        break;
                    case 6:
                        CaraWayne = 0x37;
                        break;
                    case 7:
                        CaraWayne = 0x3F;
                        break;
                    case 8:
                        CaraWayne = 0x47;
                        break;
                    case 9:
                        CaraWayne = 0x4F;
                        break;
                    case 10:
                        CaraWayne = 0x57;
                        break;
                    case 11:
                        CaraWayne = 0x5F;
                        break;
                    case 12:
                        CaraWayne = 0x67;
                        break;
                    case 13:
                        CaraWayne = 0x6F;
                        break;
                    case 14:
                        CaraWayne = 0x77;
                        break;
                    case 15:
                        CaraWayne = 0x7F;
                        break;
                    case 16:
                        CaraWayne = 0x87;
                        break;
                }
                return CaraWayne;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|AsignacionCaraClaseIII: " + Excepcion);
                SWRegistro.Flush();
                return 0;
            }
        }

        //ENVIA EL COMANDO AL SURTIDOR
        private void EnviarComando()
        {
            try
            {
                //Limpia todo lo que este en el Buffer de salida y Buffer de entrada del puerto
                PuertoCom.DiscardOutBuffer();
                PuertoCom.DiscardInBuffer();

                //Escribe en el puerto el comando a Enviar.
                PuertoCom.Write(TramaTx, 0, TramaTx.Length);

                /////////////////////////////////////////////////////////////////////////////////
                //LOGUEO DE TRAMA TRANSMITIDA
                if (LogueoTramas)//DCF 20/03/2018 Logueo Tramas
                {
                    string strTrama = "";
                    for (int i = 0; i <= TramaTx.Length - 1; i++)
                        strTrama += TramaTx[i].ToString("X2") + "|";

                    SWTramas.WriteLine(
                        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                        DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                        "|" + CaraID + "|Tx|" + ComandoCaras + "|" + strTrama);

                    SWTramas.Flush();
                }
                ///////////////////////////////////////////////////////////////////////////////////                         
                //Tiempo muerto mientras el Surtidor Responde
                //Thread.Sleep(TimeOut + 2000); //DCF Borra solo para testeo
                Thread.Sleep(TimeOut);

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|EnviarComando: " + Excepcion);
                SWRegistro.Flush();
            }
        }


        //Envia comandos por TCPIP
        public void EnviarComando_TCPIP()
        {
            try
            {
                try
                {
                    Stream.Write(TramaTx, 0, TramaTx.Length);
                    Stream.Flush();
                }
                catch (System.Net.Sockets.SocketException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:   Sockets.SocketException ");
                        SWTramas.Flush();

                    }
                    catch (Exception)
                    {
                        VerificarConexion();
                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + ComandoCaras );
                        SWTramas.Flush();

                    }
                }
                catch (System.IO.IOException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:   VerificarConexion ");
                        SWTramas.Flush();

                    }
                    catch (Exception)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + ComandoCaras);
                        SWTramas.Flush();                        
                    }

                }
                catch (System.Exception)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();                        
                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + ComandoCaras);
                        SWTramas.Flush();

                    }

                }
                /////////////////////////////////////////////////////////////////////////////////
                //LOGUEO DE TRAMA TRANSMITIDA
                if (LogueoTramas)//DCF 20/03/2018 Logueo Tramas
                {
                    string strTrama = "";
                    for (int i = 0; i <= TramaTx.Length - 1; i++)
                        strTrama += TramaTx[i].ToString("X2") + "|";


                    SWTramas.WriteLine(
                        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                        DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                        "|" + CaraID + "|Tx|" + strTrama);

                    SWTramas.Flush();
                }

                //Thread.Sleep(TimeOut);//Tiempo 
                Thread.Sleep(TimeOut);//Tiempo solo para pruebas de loop 

            }
            catch (System.IO.IOException ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Reintento de envio de comando");
                SWTramas.Flush();
                SWRegistro.WriteLine(DateTime.Now + "|Reintento de envio de comando| " + ex.Message);
                SWTramas.Flush();
            }
        }



        //LEE Y ALMACENA LA TRAMA RECIBIDA
        private void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;

                //WBeleno: Comentado 2015.08.05
                //if (!TramaEco)
                //    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                int BytesEsperados = 0x0D + eco;

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                if (Bytes >= BytesEsperados)
                {
                    //Definicion de Trama Temporal
                    byte[] TramaTemporal = new byte[Bytes];

                    //Almacena informacion en la Trama Temporal para luego eliminarle el eco
                    PuertoCom.Read(TramaTemporal, 0, Bytes);
                    PuertoCom.DiscardInBuffer();

                    //Se dimensiona la Trama a evaluarse (TramaRx)
                    TramaRx = new byte[TramaTemporal.Length - eco];

                    //Almacena los datos reales (sin eco) en TramaRx
                    for (int i = 0; i <= (TramaTemporal.Length - eco - 1); i++)
                        TramaRx[i] = TramaTemporal[i + eco];

                    /////////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA RECIBIDA
                    if (LogueoTramas)//DCF 20/03/2018 Logueo Tramas
                    {
                        string strTrama = "";
                        for (int i = 0; i <= TramaRx.Length - 1; i++)
                            strTrama += TramaRx[i].ToString("X2") + "|";

                        SWTramas.WriteLine(
                            DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                            DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                            DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                            DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                             "|" + CaraID + "|Rx|" + ComandoCaras + "|" + strTrama);

                        SWTramas.Flush();
                    }
                    ///////////////////////////////////////////////////////////////////////////////////

                    //Revisa si existe problemas en la trama
                    if (ComprobarIntegridadTrama())
                    {
                        FalloComunicacion = false; //DCF 20110117
                        AnalizarTrama();
                    }

                    else
                    {
                        FalloComunicacion = true;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Comando " + ComandoCaras + ". Bytes con fallo en integridad de trama");
                        SWRegistro.Flush();
                    }
                }
                else if (FalloComunicacion == false)
                {
                    FalloComunicacion = true;
                    if (!PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|" + ComandoCaras + ". Bytes Esperados: " + BytesEsperados + " - Bytes Recibidos: " + Bytes);
                        SWRegistro.Flush();
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|RecibirInformacion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void RecibirInformacion_TCPIP()
        {
            try
            {

                //WBeleno: Comentado 2015.08.05
                //if (!TramaEco)
                //    eco = 0;               

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                int BytesEsperados = 0x0D + eco;

               // byte[] TramaRxTemporal = new byte[BytesEsperados];
                byte[] TramaRxTemporal = new byte[255];

                if (Stream == null)
                {
                    FalloComunicacion = true;
                    return;
                }

                if (!Stream.DataAvailable)
                    Thread.Sleep(40);

                if (Stream.DataAvailable)
                {     
                    // Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                    if (Stream.CanRead)
                    {
                        do
                        {
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);
                        } while (Stream.DataAvailable);

                        
                       //     //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                       //     Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);
                       //if (Stream.DataAvailable)
                       //   {
                       //    int Bytes_leidos2 = 
                       //     }

                    }


                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                   if (Bytes_leidos >= BytesEsperados )
                    {
                        //LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                        FalloComunicacion = false;


                        //Definicion de Trama Temporal
                        byte[] TramaTemporal = new byte[Bytes_leidos];

                        //Se dimensiona la Trama a evaluarse (TramaRx)
                        TramaRx = new byte[TramaTemporal.Length - eco];

                        //Almacena los datos reales (sin eco) en TramaRx
                        for (int i = 0; i <= (TramaTemporal.Length - eco - 1); i++)
                            TramaRx[i] = TramaRxTemporal[i + eco];


                        ///////////////////////////////////////////////////////////////////////////////
                        //LOGUEO DE TRAMA RECIBIDA
                        if (LogueoTramas)//DCF 20/03/2018 Logueo Tramas
                        {
                            string strTrama = "";
                            for (int i = 0; i <= TramaRx.Length - 1; i++)
                                strTrama += TramaRx[i].ToString("X2") + "|";


                            SWTramas.WriteLine(
                                DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                                DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                "|" + CaraID + "|Rx|" + strTrama);

                            SWTramas.Flush();
                        }
                        /////////////////////////////////////////////////////////////////////////////////

                        AnalizarTrama();

                    }
                    else if (FalloComunicacion == false)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Error|" + " Bytes_leidos = " + Bytes_leidos + " | BytesEsperados = |" + BytesEsperados);
                        SWRegistro.Flush();

                        FalloComunicacion = true;
                    }

                    //SWRegistro.Flush();
                }
                else if (FalloComunicacion == false)
                {
                    FalloComunicacion = true;
                }


            }
            catch (Exception Excepcion)
            {
                LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                string MensajeExcepcion = "Excepcion en el Metodo RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public void VerificarConexion()
        {
            int iReintento = 0;
            string Comando = "";
            try
            {
                if (ClienteWayne == null)
                {
                    Boolean EsInicializado = false;
                   
                    while (!EsInicializado)
                    {
                        try
                        {
                            
                            ClienteWayne = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                            

                            if (ClienteWayne == null)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|No inicializo - Ip: " + DireccionIP + " Puerto: " + Puerto);
                                SWRegistro.Flush();
                                Thread.Sleep(200);
                            }
                        }
                        catch (Exception e)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla de inicializacion - Ip: " + DireccionIP + " Puerto: " + Puerto + " Mensaje: " + e.Message);
                            SWRegistro.Flush();
                            Thread.Sleep(200);
                        }

                        if (ClienteWayne != null)
                        {
                            //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Inicializada - Ip: " + DireccionIP + " Puerto: " + Puerto);
                            //SWRegistro.Flush();
                            EsInicializado = true;
                        }
                    }
                   
                }

                Boolean estadoAnterior = true;
                if (!this.ClienteWayne.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClienteWayne.Client.BeginDisconnect(true, callBack, ClienteWayne);

                    }

                    catch (Exception e)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginDisconnect: " + e.Message);
                        SWRegistro.Flush();
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Conexion Abierta");
                    //SWRegistro.Flush();
                    estadoAnterior = true;
                }



                while (!this.ClienteWayne.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteWayne.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteWayne);
                        //ClienteWayne.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteWayne.Client.Connected)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    catch (System.Net.Sockets.SocketException)
                    {//Reintento de conexcion para el caso de Cruz roja

                        //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect-Creando Socket: " + ex.Message);
                        //SWRegistro.Flush();
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect-Creando Socket: Abriendo nuevamente la conexcion");
                        SWRegistro.Flush();

                        AbrirSocketReintento();

                    }
                    catch (Exception)
                    {
                        //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect: " + e.Message);
                        //SWRegistro.Flush();

                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect: Abriendo nuevamente la conexcion");
                        SWRegistro.Flush();

                        AbrirSocketReintento();
                    }
                }
                this.Stream = ClienteWayne.GetStream();
                if (!estadoAnterior)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Reconexion establecida");
                    SWRegistro.Flush();
                }
            }
            catch (Exception exec)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Excepcion|" + exec.Message);
                SWRegistro.Flush();
            }
        }


        void LimpiarVariableSocket()
        {
            try
            {
                ClienteWayne.Close();
                Stream.Close();
                Stream.Dispose();
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Metodo|" + "LimpiarVariableSocket: " + "Mensaje: " + ex.Message);
                SWRegistro.Flush();
            }

        }

        void AbrirSocketReintento()
        {
            try
            {
                Thread.Sleep(20);
                LimpiarVariableSocket();//Libero los recursos antes de iniciar una nueva conexcion con la veeder
                ClienteWayne = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteWayne.GetStream();
                if (this.ClienteWayne.Client.Connected == true)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Conexion Abierta");
                    SWRegistro.Flush();
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Conexion Cerrada");
                    SWRegistro.Flush();
                }


            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Falla AbrirSocketReintento  Creando Socket : " + ex.Message);
                SWRegistro.Flush();

            }

        }

        public static void CallBackMethod(IAsyncResult asyncresult)
        {

        }

        public void LimpiarSockets()
        {
            try
            {
                //ClienteWayne.Client.Disconnect(false);  
                ClienteWayne.Client.Close();
                ClienteWayne.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteWayne = null;
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|LimpiarSockets:" + ex.Message);
                SWRegistro.Flush();

            }

        }







        //REVISA LA INTEGRIDAD DE LA TRAMA
        private bool ComprobarIntegridadTrama()
        {
            try
            {
             
                if (TramaRx[0] == 0x00 && TramaRx[1] == 0x00) //Inicio de Trama 0x00 0x00
                {
                  
                    if (TramaRx[12] == 0xFF) 
                    {
                        for (int i = 2; i <= 10; i += 2)
                        {
                            if (TramaRx[i + 1] != 0xFF - TramaRx[i])
                            {

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|************ return false   " + TramaRx[12].ToString("X2"));
                                SWRegistro.Flush();

                                return false;

                            }
                        }
                        return true;
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fin de Trama Erroneo: " + TramaRx[12].ToString("X2"));
                        SWRegistro.Flush();
                        return false;
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Inicio de Trama Erroneo: " + TramaRx[0].ToString("X2") +
                        " " + TramaRx[1].ToString("X2"));
                    SWRegistro.Flush();
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ComprobacionIntegridadTrama: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        #endregion

        #region ANALISIS DE TRAMAS Y RECONSTRUCCIÓN DE DATOS PROVENIENTE DEL SURTIDOR

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void AnalizarTrama()
        {
            try
            {
                switch (ComandoCaras)
                {
                    case ComandoSurtidor.Estado:
                        RecuperarEstado();
                        break;

                    case ComandoSurtidor.EstalecerPrecio:
                    case ComandoSurtidor.ObtenerPrecio:
                        //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 =
                        //    Convert.ToDecimal(Convert.ToInt32(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2"), 16)) /
                        //    Convert.ToDecimal(PropiedadesCara[CaraEncuestada].FactorPrecio); // 


                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 =
                            Convert.ToDecimal(Convert.ToInt32(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2"), 16) *
                            PropiedadesCara[CaraEncuestada].MultiplicadorPrecioVenta) /
                            Convert.ToDecimal(PropiedadesCara[CaraEncuestada].FactorPrecio);


                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|ObtenerPrecio: " + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1);
                        //SWRegistro.Flush(); //DCF Borra

                        break;

                    case ComandoSurtidor.ObtenerVentaDinero:
                        PropiedadesCara[CaraEncuestada].TotalVenta =
                            Convert.ToDecimal(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2") + TramaRx[6].ToString("X2")) /
                            PropiedadesCara[CaraEncuestada].FactorImporte;
                        break;

                    case ComandoSurtidor.ObtenerVentaVolumen:
                        PropiedadesCara[CaraEncuestada].Volumen =
                            Convert.ToDecimal(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2") + TramaRx[6].ToString("X2")) /
                            PropiedadesCara[CaraEncuestada].FactorVolumen;
                        break;

                    case ComandoSurtidor.ObtenerTotalizador_I:
                        AuxiliarLectura = TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2");
                        break;
                    case ComandoSurtidor.ObtenerTotalizador_II:
                        AuxiliarLectura = AuxiliarLectura + TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2");
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura =
                            Convert.ToDecimal(AuxiliarLectura) / PropiedadesCara[CaraEncuestada].FactorTotalizador;
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|AnalizarTrama: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void RecuperarEstado()
        {
            try
            {
                /*Estados: 
                -	07 Mangueras colgadas
                -	00 Manguera 1 descolgada
                -	01 Manguera 2 descolgada
                -	02 Manguera 3 descolgada
                -	03 Manguera 4 descolgada
                -	0F Cara preseteada
                -	8X :
                o	X=8 manguera 1 despacho
                o	X=9 manguera 2 despacho
                o	X=A Manguera 3 despacho
                o	X=B Manguera 4 despacho
                o	X=F Termino la carga                
               */

                //Almacena en archivo el estado actual del surtidor
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                    PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;

                //Asigna Estado




                if (TramaRx[12] == 0xFF)
                {
                    switch (TramaRx[10])
                    {
                        case (0x07):
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.WayneDespacho ||
                                PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                            {
                                PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneFinDespachoForzado;

                                //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                                //esto funciona siempre y cuando no se hagan mas venta en la cara.
                                //if (Convert.ToInt32(PropiedadesCara[CaraEncuestada].GradoMangueraVentaParcial) >= 0) //24-11-2016 DCF
                                //{

                                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|******* PropiedadesCara[CaraEncuestada].GradoVenta = : " + PropiedadesCara[CaraEncuestada].GradoVenta + " *** PropiedadesCara[CaraEncuestada].GradoMangueraVentaParcial = " + PropiedadesCara[CaraEncuestada].GradoMangueraVentaParcial);
                                //    SWRegistro.Flush();


                                //    PropiedadesCara[CaraEncuestada].GradoVenta = Convert.ToInt32(PropiedadesCara[CaraEncuestada].GradoMangueraVentaParcial);
                                //    PropiedadesCara[CaraEncuestada].GradoMangueraVentaParcial = "-1";


                                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|******* PropiedadesCara[CaraEncuestada].GradoVenta = : " + PropiedadesCara[CaraEncuestada].GradoVenta + " *** PropiedadesCara[CaraEncuestada].GradoMangueraVentaParcial = " + PropiedadesCara[CaraEncuestada].GradoMangueraVentaParcial);
                                //    SWRegistro.Flush();


                                //}

                            }
                            else
                                PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneReposo;
                            break;

                        case (0x00)://Manguera 1 Descolgada
                        case (0x01)://Manguera 2 Descolgada
                        case (0x02)://Manguera 3 Descolgada
                        case (0x03)://Manguera 4 Descolgada
                            //PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDescolgada;

                            //Obtener Grado de la cara
                            //PropiedadesCara[CaraEncuestada].GradoVenta = TramaRx[10];

                            //Obtener Grado de la cara
                            if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespacho && PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDescolgada)
                                PropiedadesCara[CaraEncuestada].GradoVenta = TramaRx[10];


                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDescolgada;
                            break;

                        case (0x88)://Manguera 1 Despachando
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                            PropiedadesCara[CaraEncuestada].GradoVenta = 0;
                            break;

                        case (0x89)://Manguera 2 Despachando
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                            PropiedadesCara[CaraEncuestada].GradoVenta = 1;
                            break;

                        case (0x8A)://Manguera 3 Despachando
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                            PropiedadesCara[CaraEncuestada].GradoVenta = 2;
                            break;

                        case (0x8B)://Manguera 4 Despachando
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                            PropiedadesCara[CaraEncuestada].GradoVenta = 3;
                            break;

                        case (0x8F)://termino la Carga
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneFinDespacho;
                            break;

                        case (0xF1)://Autorizado listo para la vender
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespachoAutorizado;
                            break;

                        case (0xF5)://Autorizado listo para la vender
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneBloqueada;
                            break;

                        case (0x0F):
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WaynePredeterminada;
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.WaynePredeterminada)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado Cara|" + PropiedadesCara[CaraEncuestada].Estado.ToString());
                                SWRegistro.Flush();
                            }

                            break;
                        default:
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Estado Indeterminado: " + TramaRx[10].ToString("X2"));
                            SWRegistro.Flush();
                            break;
                    }
                }

                PropiedadesCara[CaraEncuestada].Volumen = Convert.ToDecimal(Convert.ToInt32(TramaRx[6].ToString("X2") + TramaRx[4].ToString("X2"), 16)) /
                    PropiedadesCara[CaraEncuestada].FactorVolumen;
                PropiedadesCara[CaraEncuestada].TotalVenta = PropiedadesCara[CaraEncuestada].Volumen *
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioNivel1;

                //Almacena en archivo el estado actual del surtidor
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + PropiedadesCara[CaraEncuestada].Estado.ToString());

                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|RecuperarEstado: " + Excepcion);
                SWRegistro.Flush();
            }
        }



        #endregion

        #region PROCESOS DE TOMA DE DECISIONES SEGÚN ESTADOS DE LA CARA

        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        private void TomarAccion()
        {
            try
            {
                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (PropiedadesCara[CaraEncuestada].Estado)
                {
                    case (EstadoCara.WayneReposo):
                        //Informa cambio de estado
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                        {
                            int IdManguera =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;

                            if (AplicaWindows)
                            {
                                if (CaraEnReposo != null)
                                {
                                    CaraEnReposo(CaraID, IdManguera);
                                }
                            }


                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa cara en Espera. Manguera " + IdManguera);
                            SWRegistro.Flush();
                        }

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true || PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            LecturaAperturaCierre();



                        //ORedSurtidor.CambiarProductoAMangueras = true;
                        //Revisa si se tiene que hacer cambio de producto en alguna manguera de la cara
                        if (PropiedadesCara[CaraEncuestada].CambiarProductoAMangueras)// New DCF 2011-02-11
                        {
                            //Revisando en que grados hay que cambiar el producto
                            foreach (Grados OGrado in PropiedadesCara[CaraEncuestada].ListaGrados)
                            {


                                //Si hay cambio de producto se realiza el cambio de precio
                                if (OGrado.CambiarProducto)
                                {
                                    //Si se aplica satisfactoriamente el cambio de precio se hace efectivo el cambio de producto
                                    if (CambiarPreciosEnGrado(OGrado))
                                    {
                                        int MangueraANotificar = OGrado.MangueraBD;
                                        OGrado.Autorizar = true;
                                        OGrado.IdProducto = OGrado.IdProductoACambiar;

                                        if (AplicaWindows)
                                        {
                                            if (NotificarCambioPrecioManguera != null)
                                            {
                                                NotificarCambioPrecioManguera(MangueraANotificar);
                                            }
                                        }
                                        else



                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa cambio de precio Manguera: " +
                                                OGrado.MangueraBD + " - Grado: " + OGrado.IdProducto);
                                        SWRegistro.Flush();
                                    }
                                    else
                                    {
                                        //Se informa el estado de no autorizacion para la manguera a la cual no se le pudo
                                        //hacer el cambio de precio
                                        //OGrado.Autorizar = false;
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Informa cambio de precio Manguera: " +
                                           OGrado.MangueraBD + ", Desactivada por problema al cambiar producto");
                                        SWRegistro.Flush();
                                    }
                                    OGrado.CambiarProducto = false;
                                }
                            }

                            PropiedadesCara[CaraEncuestada].CambiarProductoAMangueras = false;
                        }



                        break;

                    case (EstadoCara.WayneDescolgada):
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Manguera descolgada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                            PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                        {
                            PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta; //se asigna el grado que despacha
                            if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                            {
                                if (ProcesoTomaLectura())
                                {
                                    int IdProducto =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto;
                                    int IdManguera =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;
                                    string Lectura =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura.ToString("N3");



                                    // -- Modificado 2012.04.23-0901
                                    SWRegistro.WriteLine(
                                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                    DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
                                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                     " |" + CaraID + "|  Antes de Enviar oEvento.RequerirAutorizacion. - Grado "
                                        + PropiedadesCara[CaraEncuestada].GradoAutorizado + " - Producto: " +
                                        IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura);
                                    SWRegistro.Flush();// -- Modificado 2012.04.23-0901



                                    if (AplicaWindows)
                                    {
                                        if (AutorizacionRequerida != null)
                                        {
                                            AutorizacionRequerida(CaraID, IdProducto, IdManguera, Lectura,"");
                                        }
                                    }



                                    SWRegistro.WriteLine(
                                   DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                   DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
                                   DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                   DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                    " |" + CaraID + "|  Despues de lanzar oEvento.RequerirAutorizacion");
                                    SWRegistro.Flush();// -- Modificado 2012.04.23-0901



                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa requerimiento de autorizacion. Grado: "
                                        + PropiedadesCara[CaraEncuestada].GradoVenta + " - Producto: " +
                                        IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura + " - Precio: " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador para Lectura Inicial Venta");
                                    SWRegistro.Flush();
                                }
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Precio para inicio de venta");
                                SWRegistro.Flush();
                            }
                        }
                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura;

                            string strLecturasVolumen =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");

                            if (AplicaWindows)
                            {
                                if (LecturaInicialVenta != null)
                                {
                                    LecturaInicialVenta(CaraID, strLecturasVolumen);
                                }
                            }



                            //Loguea Evento de envio de lectura
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Lectura Inicial de Venta: " +
                                strLecturasVolumen);
                            SWRegistro.Flush();

                            if (PropiedadesCara[CaraEncuestada].PredeterminarVolumen)
                            {
                                //PropiedadesCara[CaraEncuestada].ValorPredeterminado = PropiedadesCara[CaraEncuestada].ValorPredeterminado
                                //    * PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioNivel1;

                                //PropiedadesCara[CaraEncuestada].PredeterminarImporte = true;
                                //PropiedadesCara[CaraEncuestada].PredeterminarVolumen = false;

                                //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Predeterminacion Volumen.  Conversion a dinero: " +
                                //        PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                                //SWRegistro.Flush();

                                if (ProcesoEnvioComando(ComandoSurtidor.Preset_Volumen))// DCF 28/08/2018 Preset por Volumen Cartago  
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Predeterminacion exitosa. Volumen: " +
                                        PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Proceso de predetermiancion de Volumen fallido");
                                    SWRegistro.Flush();
                                }


                            }

                            //Valor de Predeterminacion en $$
                            if (PropiedadesCara[CaraEncuestada].PredeterminarImporte)
                            {
                                if (ProcesoEnvioComando(ComandoSurtidor.Predeterminar))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Predeterminacion exitosa. Importe: " +
                                        PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Proceso de predetermiancion fallido");
                                    SWRegistro.Flush();
                                }
                            }

                            int Reintenos = 1;
                            do
                            {
                                if (!ProcesoEnvioComando(ComandoSurtidor.Autorizar))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondió comando de Autorizar Despacho");
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Comando Autorizacion enviado con exito");
                                    SWRegistro.Flush();
                                }
                                Thread.Sleep(100);//DCF para actualizar estado
                                ProcesoEnvioComando(ComandoSurtidor.Estado);
                                Reintenos++;
                            } while (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespacho &&
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespachoAutorizado &&
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneReposo &&
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WaynePredeterminada && Reintenos <= 2);//No Autorizar con manguera colgada:  PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneReposo  Wayne 2011.06.03-1134

                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            //if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneDespachoAutorizado ||
                            //    PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneDespacho)

                            //corregir las pérdidas de venta por el estado (0F) WaynePredeterminada con manguera levantada
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneDespachoAutorizado ||
                                PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneDespacho ||
                                PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WaynePredeterminada)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Comando Autorizacion Aceptado");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = true;

                                if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WaynePredeterminada)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Despacho con WaynePredeterminada");
                                    SWRegistro.Flush();
                                }

                            }
                            else if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneReposo)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Manguera colgada luego de autorizada");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false;


                                if (AplicaWindows)
                                {
                                    if (VentaInterrumpidaEnCero != null)
                                    {
                                        VentaInterrumpidaEnCero(CaraID);
                                    }
                                }


                            }
                            else if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespachoAutorizado &&
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespacho)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Comando Autorizacion No aceptado");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false;//DCF 29-07-2011 
                                //PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                            }


                        }

                        break;

                    case EstadoCara.WayneDespachoAutorizado:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara Autorizada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                        break;

                    case EstadoCara.WayneFinDespacho_AF:
                    case EstadoCara.WayneFinDespachoForzado:
                    case EstadoCara.WayneFinDespacho: //NEW en gas norte

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en fin de despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma deLecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma deLecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                            ProcesoFindeVenta();
                        break;

                    case EstadoCara.WayneDespacho:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en Despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = true;

                        //Reporta los valores de parciales de despacho                
                        string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                        string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");

                        if (AplicaWindows)
                        {
                            if (VentaParcial != null)
                            {
                                VentaParcial(CaraID, strTotalVenta, strVolumen);
                            }
                        }


                        // SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|strTotalVenta = " + strTotalVenta + "  |strVolumen =" + strVolumen);
                        // SWRegistro.Flush(); //DCF Borra porbar parciales de despacho  

                        break;

                    case EstadoCara.WayneBloqueada:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara Bloqueada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Se envía comandos para desbloquear manguera
                        foreach (Grados Grado in PropiedadesCara[CaraEncuestada].ListaGrados) //Recorre las mangueras de la cara encuestada
                        {
                            PropiedadesCara[CaraEncuestada].GradoCara = Grado.NoGrado;

                            //Obtiene lecturas
                            if (!ProcesoTomaLectura())
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador para Desbloqueo Cara. Grado: " + Grado.NoGrado);
                                SWRegistro.Flush();
                            }

                            //Obtiene Precio
                            if (!ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener precio para Desbloqueo Cara. Grado: " + Grado.NoGrado);
                                SWRegistro.Flush();
                            }
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Toma lecturas y precios para desbloqueo de Cara. Manguera:  " +
                                PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura: " +
                                PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].Lectura + " - Precio: " +
                                PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioSurtidorNivel1);
                            SWRegistro.Flush();
                        }
                        break;


                    case EstadoCara.WaynePredeterminada: //DCF

                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.WaynePredeterminada)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado Cara..|" + PropiedadesCara[CaraEncuestada].Estado.ToString());
                            SWRegistro.Flush();
                        }
                        break;




                    default:

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Estado Indeterminado: " + PropiedadesCara[CaraEncuestada].Estado);
                        SWRegistro.Flush();
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|TomarAccion: " + Excepcion);
                SWRegistro.Flush();
            }
        }


        private bool CambiarPreciosEnGrado(Grados grado)
        {
            bool CambioPrecios = false;

            try
            {
                //Variable que indica qué precio cambiar
                //bool PrecioNivel1 = new bool();
                int Reintentos = 0;

                //Si hay cambio de precio pendiente, lo aplica
                for (int i = 0; i <= PropiedadesCara[CaraEncuestada].ListaGrados.Count - 1; i++)
                {
                    if (PropiedadesCara[CaraEncuestada].ListaGrados[i].NoGrado == grado.NoGrado)
                    {
                        PropiedadesCara[CaraEncuestada].GradoCara = i;


                        //PrecioNivel1 = true;
                        Reintentos = 0;
                        do
                        {

                            if (ProcesoEnvioComando(ComandoSurtidor.EstalecerPrecio))
                            {
                                if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 ==
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio aceptado por cara. Grado " +
                                        PropiedadesCara[CaraEncuestada].GradoCara + " - Precio: " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                                    SWRegistro.Flush();

                                    CambioPrecios = true;
                                    break;
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 1: Precio del Surtidor: " +
                                               PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 +
                                               " - Precio Requerido: " + PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioNivel1 + " - Reintentos: " + Reintentos);
                                    SWRegistro.Flush();

                                    //REPORTANDO EL CAMBIO DE PRECIO FALLIDO
                                    int Manguera = PropiedadesCara[CaraEncuestada].ListaGrados[i].MangueraBD;
                                    double Precio = Convert.ToDouble(PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioNivel1);


                                    if (AplicaWindows)
                                    {
                                        if (CambioPrecioFallido != null)
                                        {
                                            CambioPrecioFallido(Manguera, Precio);
                                        }
                                    }

                                }


                            }
                            else
                            {
                                Reintentos += 1;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando Establecer Precio");
                                SWRegistro.Flush();

                                //REPORTANDO EL CAMBIO DE PRECIO FALLIDO
                                int Manguera = PropiedadesCara[CaraEncuestada].ListaGrados[i].MangueraBD;
                                double Precio = Convert.ToDouble((PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioNivel1));


                                if (AplicaWindows)
                                {
                                    if (CambioPrecioFallido != null)
                                    {
                                        CambioPrecioFallido(Manguera, Precio);
                                    }
                                }

                            }

                        } while (Reintentos <= 3);

                    }
                }

                //Si pudo cambiar ambos precios en el grado, se devuelve un cambio de precio exitoso
                if (CambioPrecios)
                    return true;
                else
                    return false;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CambiarPreciosEnGrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        } //New



        private bool ProcesoTomaLectura()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador_I))
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador_II))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Toma de lecturas Exitoso: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura);
                        SWRegistro.Flush();
                        return true;
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador_II");
                        SWRegistro.Flush();
                        return false;
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador_I");
                    SWRegistro.Flush();
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoTomaLectura: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //REALIZA PROCESO DE FIN DE VENTAF
        private void ProcesoFindeVenta()
        {
            try
            {
                //Inicializacion de variables
                PropiedadesCara[CaraEncuestada].Volumen = 0;
                PropiedadesCara[CaraEncuestada].TotalVenta = 0;
                PropiedadesCara[CaraEncuestada].PrecioVenta = 0;


                //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                if (PropiedadesCara[CaraEncuestada].GradoCara != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 20110117
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado autorizado: " + PropiedadesCara[CaraEncuestada].GradoCara +
                        " - Grado que vendio: " + PropiedadesCara[CaraEncuestada].GradoVenta);
                    SWRegistro.Flush();
                }

                PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta; //Actualizo el grado que despacho


                int conteo = 0;
                difer = 0;

                do
                {
                    if (!ProcesoTomaLectura())
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de totalizadores para Lectura Final de Venta");
                        SWRegistro.Flush();

                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta = 0; //DCF

                        break;

                    }
                    else
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura;

                    conteo++;

                    difer = (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura -
                           PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta);

                    if (difer < 0)
                    {
                        Thread.Sleep(200); //para que refresque la lectura para terpel
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|ERROR| ** Totalizador Entregado menor que Li ** LF = " +
                              PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura);
                        SWRegistro.Flush();
                    }

                } while ((difer) < 0 && conteo < 3); //Reintento en los totalizadores con retroceso y calcular lectura final vol + Li 18/04/2013



                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                {
                    PropiedadesCara[CaraEncuestada].PrecioVenta =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1;


                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerVentaDinero))
                    {
                        if (ProcesoEnvioComando(ComandoSurtidor.ObtenerVentaVolumen))
                        {
                            //Reintento en los totalizadores con retroceso y calcular lectura final vol + Li 18/04/2013
                            if (difer < 0) //DCF 18/04/2013 indica que la lectura final es menor a la inicial y se recalcula con el volumen despachado + lectura Inicial.
                            {
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                                   (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta +
                                    PropiedadesCara[CaraEncuestada].Volumen);

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia| **** Se Calcula Lectura Final = LecturaInicialVenta (" + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta + ")"
                                    + " + Volumen (" + PropiedadesCara[CaraEncuestada].Volumen + ") = " + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta);
                                SWRegistro.Flush();

                            }


                            ////Calcula la correspondencia entre el Volumen, el Precio y el Importe //DCF
                            decimal TotalVentaCalculada = PropiedadesCara[CaraEncuestada].Volumen *
                                PropiedadesCara[CaraEncuestada].PrecioVenta; //

                            decimal PorcentajeVenta = (TotalVentaCalculada * (Convert.ToDecimal(2))) / 100;   // se Incrementa al 2% TotalVentaCalculada 30/07/2012 //Porcentaje del 1% //DCF 10-04-2012 corrección para Perú  ventas 9,999 
                            if (PropiedadesCara[CaraEncuestada].TotalVenta == 0 ||
                                PropiedadesCara[CaraEncuestada].TotalVenta > TotalVentaCalculada + (PorcentajeVenta) ||
                                PropiedadesCara[CaraEncuestada].TotalVenta < TotalVentaCalculada - (PorcentajeVenta)) // Para Peru 
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID +
                                    "|Inconsistencia|Valor recibido en : " + PropiedadesCara[CaraEncuestada].TotalVenta +
                                    " - Calculado: " + TotalVentaCalculada);
                                SWRegistro.Flush();

                                PropiedadesCara[CaraEncuestada].TotalVenta = TotalVentaCalculada;

                            }


                            //se genera un error en Bolivia no entrega totalizador Actualizado. se pregunta hasta 3 veces para que actualice la LF si entrega dato de venta Volumen 
                            int contLF = 0;

                            if (PropiedadesCara[CaraEncuestada].Volumen != 0 &&
                               (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta ==
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta))
                            {

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| ** - No entrego LF Actualizada - **");
                                do
                                {
                                    Thread.Sleep(1500);

                                    ProcesoTomaLectura();

                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura;


                                    contLF++;

                                } while (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta ==
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta && contLF < 5);
                                //
                            }

                            ////Evalúa si la venta viene en 0
                            //if (PropiedadesCara[CaraEncuestada].Volumen != 0 || PropiedadesCara[CaraEncuestada].TotalVenta != 0)

                            if ((PropiedadesCara[CaraEncuestada].Volumen != 0 || PropiedadesCara[CaraEncuestada].TotalVenta != 0) //Venta en Cero si LF = LI, indicado Por wal 
                                 &&
                                 (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta !=
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta))
                            {
                                //Almacena los valores en las variables requerida por el Evento
                                string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                                string strPrecio = PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                                string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                                string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                                string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");
                                string bytProducto = Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto);
                                int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;

                                //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false;

                                //Loguea evento Fin de Venta
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|InformarFinalizacionVenta. Importe: " + strTotalVenta +
                                    " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                                    " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                                SWRegistro.Flush();

                                String PresionLLenado = "0";

                                if (AplicaWindows)
                                {
                                    if (VentaFinalizada != null)
                                    {
                                        VentaFinalizada(CaraID, strTotalVenta, strPrecio, strLecturaFinalVenta, strVolumen, bytProducto, IdManguera, PresionLLenado, strLecturaInicialVenta);
                                    }
                                }




                                ////if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 27-07-2011 
                                ////{//Se asegura obtener siempre la lectura Inicial de volumen, para corregir el error de lecturas, en caso que el grado autorizado no sea el que despacho
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta;


                            }
                            else
                            {


                                if (AplicaWindows)
                                {
                                    if (VentaInterrumpidaEnCero != null)
                                    {
                                        VentaInterrumpidaEnCero(CaraID);
                                    }
                                }


                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Venta en CERO");
                                SWRegistro.Flush();


                                //DCF 

                                if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta ==
                               PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta) ////Venta en Cero si LF = LI, indicado Por wal 
                                {
                                    string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                                    string strPrecio = PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                                    string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                                    string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                                    string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");
                                    byte bytProducto = Convert.ToByte(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto);
                                    int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;


                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Seguimiento|LecturaInicialVenta = LecturaFinalVenta. Importe: " + strTotalVenta +
                                      " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                                      " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                                    SWRegistro.Flush();
                                }

                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de datos de Final de Venta Volumen");
                            SWRegistro.Flush();
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de datos de Final de Venta Importe");
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de Precio en Final de Venta");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoFindeVenta: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void LecturaAperturaCierre()
        {
            try
            {
                bool TomaLecturasExitoso = true;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();

                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                foreach (Grados Grado in PropiedadesCara[CaraEncuestada].ListaGrados) //Recorre las mangueras de la cara encuestada
                {
                    PropiedadesCara[CaraEncuestada].GradoCara = Grado.NoGrado; //DCF para tomar los totalizadores de todas las mangueras 

                    if (ProcesoTomaLectura())
                    {
                        //Arma Arreglo de lecturas
                        ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].Lectura) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioSurtidorNivel1));

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Arma Lecturas para turno. Manguera " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].Lectura);


                        SWRegistro.Flush();

                        //Cambia el precio si es apertura de turno
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia cambio de precios");
                            SWRegistro.Flush();

                            ProcesoCambioPrecio();
                        }
                    }
                    else
                    {
                        TomaLecturasExitoso = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador para Lectura Inicial/Final de Turno. Grado: " + Grado.NoGrado);
                        SWRegistro.Flush();
                        break;
                    }
                }

                if (TomaLecturasExitoso)
                {
                    System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                    ArrayLecturas.CopyTo(LecturasEnvio);

                    //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                    if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                    {

                        if (AplicaWindows)
                        {
                            if (LecturaTurnoCerrado != null)
                            {
                                LecturaTurnoCerrado(LecturasEnvio);
                            }
                        }



                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Finales de turno");
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                    }
                    //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                    if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                    {

                        if (AplicaWindows)
                        {
                            if (LecturaTurnoAbierto != null)
                            {
                                LecturaTurnoAbierto(LecturasEnvio);
                            }
                        }


                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Iniciales de turno");
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|LecturaAperturaCierre: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void ProcesoCambioPrecio()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Cambio Precio Grado " +
                    PropiedadesCara[CaraEncuestada].GradoCara + " - Precio: " +
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                SWRegistro.Flush();

                if (ProcesoEnvioComando(ComandoSurtidor.EstalecerPrecio))
                {
                    if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 ==
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio aceptado por cara. Grado " +
                            PropiedadesCara[CaraEncuestada].GradoCara + " - Precio: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio rechazado por cara. Grado " +
                            PropiedadesCara[CaraEncuestada].GradoCara);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando Establecer Precio");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoCambioPrecio: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        #endregion

        #region EVENTOS DE LA CLASE

       //private void oEvento_VentaAutorizada(ref byte Cara, ref string Precio, ref string ValorProgramado, ref byte TipoProgramacion, ref string Placa, ref int MangueraProgramada, ref bool EsVentaGerenciada)
       public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string Guid, Decimal PresionLLenado)
     //public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string Guid)
        {
            try
            {

                // -- Modificado 2012.04.23-0901
                SWRegistro.WriteLine(
                DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
                DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 " |" + Cara + "|  Recibe oEvento_VentaAutorizada....");
                SWRegistro.Flush();// -- Modificado 2012.04.23-0901




                CaraTmp = ConvertirCaraBD(Cara);

                if (PropiedadesCara.ContainsKey(CaraTmp))
                {
                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                                            " - Gerenciada: " + EsVentaGerenciada);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para despachar
                    PropiedadesCara[CaraTmp].AutorizarCara = true; //se activa sin abrir turno ???

                    //Valor a programar
                    PropiedadesCara[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    PropiedadesCara[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

                    PropiedadesCara[CaraTmp].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (PropiedadesCara[CaraTmp].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            PropiedadesCara[CaraTmp].PredeterminarImporte = false;
                            PropiedadesCara[CaraTmp].PredeterminarVolumen = true;
                        }
                        else
                        {
                            PropiedadesCara[CaraTmp].PredeterminarImporte = true;
                            PropiedadesCara[CaraTmp].PredeterminarVolumen = false;
                        }
                    }
                    else
                    {
                        PropiedadesCara[CaraTmp].PredeterminarImporte = false;
                        PropiedadesCara[CaraTmp].PredeterminarVolumen = false;
                    }
                }



                else
                {
                    // -- Modificado 2012.04.23-0901
                    SWRegistro.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                     " |" + Cara + "|La Cara está Fuera de la red de Surtidores..");
                    SWRegistro.Flush();// -- Modificado 2012.04.23-0901

                    ////Loguea evento                
                    //SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|La Cara está Fuera de la red de Surtidores..");
                    //SWRegistro.Flush();
                }






            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEvento_VentaAutorizada: " + Excepcion);
                SWRegistro.Flush();
            }
        }


       // public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
        // public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada)
      
        //{
        //    try
        //    {

        //        // -- Modificado 2012.04.23-0901
        //        SWRegistro.WriteLine(
        //        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
        //        DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
        //        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
        //        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
        //         " |" + Cara + "|  Recibe oEvento_VentaAutorizada....");
        //        SWRegistro.Flush();// -- Modificado 2012.04.23-0901




        //        CaraTmp = ConvertirCaraBD(Cara);

        //        if (PropiedadesCara.ContainsKey(CaraTmp))
        //        {
        //            //Loguea evento                
        //            SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
        //                                    " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
        //                                    " - Gerenciada: " + EsVentaGerenciada);
        //            SWRegistro.Flush();

        //            //Bandera que indica que la cara debe autorizarse para despachar
        //            PropiedadesCara[CaraTmp].AutorizarCara = true; //se activa sin abrir turno ???

        //            //Valor a programar
        //            PropiedadesCara[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

        //            PropiedadesCara[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

        //            PropiedadesCara[CaraTmp].EsVentaGerenciada = EsVentaGerenciada;

        //            //Si viene valor para predeterminar setea banderas
        //            if (PropiedadesCara[CaraTmp].ValorPredeterminado != 0)
        //            {
        //                //1 predetermina Volumen, 0 predetermina Dinero
        //                if (TipoProgramacion == 1)
        //                {
        //                    PropiedadesCara[CaraTmp].PredeterminarImporte = false;
        //                    PropiedadesCara[CaraTmp].PredeterminarVolumen = true;
        //                }
        //                else
        //                {
        //                    PropiedadesCara[CaraTmp].PredeterminarImporte = true;
        //                    PropiedadesCara[CaraTmp].PredeterminarVolumen = false;
        //                }
        //            }
        //            else
        //            {
        //                PropiedadesCara[CaraTmp].PredeterminarImporte = false;
        //                PropiedadesCara[CaraTmp].PredeterminarVolumen = false;
        //            }
        //        }



        //        else
        //        {
        //            // -- Modificado 2012.04.23-0901
        //            SWRegistro.WriteLine(
        //            DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
        //            DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
        //            DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
        //            DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
        //             " |" + Cara + "|La Cara está Fuera de la red de Surtidores..");
        //            SWRegistro.Flush();// -- Modificado 2012.04.23-0901

        //            ////Loguea evento                
        //            //SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|La Cara está Fuera de la red de Surtidores..");
        //            //SWRegistro.Flush();
        //        }






        //    }
        //    catch (Exception Excepcion)
        //    {
        //        SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEvento_VentaAutorizada: " + Excepcion);
        //        SWRegistro.Flush();
        //    }
        //}


        private void oEvento_TurnoAbierto(ref string Surtidores, ref string PuertoTerminal, ref System.Array Precios)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (TurnoAbierto). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Diccionario de los precios manejados por cada manguera
                Dictionary<int, Grados> Grados = new Dictionary<int, Grados>();

                //Ciclo que arma Diccionario de Grados
                foreach (string sPreciosManguera in Precios)
                {
                    //Objeto Grado para añadir al Diccionario
                    Grados PrecioGrado = new Grados();

                    string[] vPreciosGrado = sPreciosManguera.Split('|');
                    PrecioGrado.IdProducto = Convert.ToByte(vPreciosGrado[0]);
                    PrecioGrado.PrecioNivel1 = Convert.ToDecimal(vPreciosGrado[1]);
                    PrecioGrado.PrecioNivel2 = Convert.ToDecimal(vPreciosGrado[2]);
                    PrecioGrado.MangueraBD = Convert.ToInt16(vPreciosGrado[3]);


                    //Si La Manguera no existe dentro del diccionario, lo añade
                    if (!Grados.ContainsKey(PrecioGrado.MangueraBD))
                        Grados.Add(PrecioGrado.MangueraBD, PrecioGrado);
                    else
                    {
                        Grados[PrecioGrado.MangueraBD].IdProducto = PrecioGrado.IdProducto;
                        Grados[PrecioGrado.MangueraBD].PrecioNivel1 = PrecioGrado.PrecioNivel1;
                        Grados[PrecioGrado.MangueraBD].PrecioNivel2 = PrecioGrado.PrecioNivel2;
                    }

                }

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;



                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                //PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel1 =
                                //    (Grados[PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1)/
                                //    PropiedadesCara[CaraEncuestada].MultiplicadorPrecioVenta; //DCF para menejo de precio con 5 digitos

                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    (Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1); //D

                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    (Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2);
                            }

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                        }

                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;



                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;


                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;
                            }

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }


                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|oEvento_TurnoAbierto: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_TurnoAbierto(string Surtidores, string PuertoTerminal, System.Array Precios)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (TurnoAbierto). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Diccionario de los precios manejados por cada manguera
                Dictionary<int, Grados> Grados = new Dictionary<int, Grados>();

                //Ciclo que arma Diccionario de Grados
                foreach (string sPreciosManguera in Precios)
                {
                    //Objeto Grado para añadir al Diccionario
                    Grados PrecioGrado = new Grados();

                    string[] vPreciosGrado = sPreciosManguera.Split('|');
                    PrecioGrado.IdProducto = Convert.ToByte(vPreciosGrado[0]);
                    PrecioGrado.PrecioNivel1 = Convert.ToDecimal(vPreciosGrado[1]);
                    PrecioGrado.PrecioNivel2 = Convert.ToDecimal(vPreciosGrado[2]);
                    PrecioGrado.MangueraBD = Convert.ToInt16(vPreciosGrado[3]);


                    //Si La Manguera no existe dentro del diccionario, lo añade
                    if (!Grados.ContainsKey(PrecioGrado.MangueraBD))
                        Grados.Add(PrecioGrado.MangueraBD, PrecioGrado);
                    else
                    {
                        Grados[PrecioGrado.MangueraBD].IdProducto = PrecioGrado.IdProducto;
                        Grados[PrecioGrado.MangueraBD].PrecioNivel1 = PrecioGrado.PrecioNivel1;
                        Grados[PrecioGrado.MangueraBD].PrecioNivel2 = PrecioGrado.PrecioNivel2;
                    }

                }

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;



                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                //PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel1 =
                                //    (Grados[PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1)/
                                //    PropiedadesCara[CaraEncuestada].MultiplicadorPrecioVenta; //DCF para menejo de precio con 5 digitos

                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    (Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1); //D

                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    (Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2);
                            }

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                        }

                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;



                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;


                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;
                            }

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }


                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|oEvento_TurnoAbierto: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void oEvento_TurnoCerrado(ref string Surtidores, ref string PuertoTerminal)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (TurnoCerrado). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;
                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaCierreTurno = true;

                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Error|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaCierreTurno = true;
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Error|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }
                    }
                }

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|oEvento_TurnoCerrado: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_TurnoCerrado(string Surtidores, string PuertoTerminal)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (TurnoCerrado). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;
                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaCierreTurno = true;

                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Error|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaCierreTurno = true;
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Error|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }
                    }
                }

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|oEvento_TurnoCerrado: " + Excepcion);
                SWRegistro.Flush();
            }
        }


        private void oEvento_FinalizarVentaPorMonitoreoCHIP(ref byte Cara)
        {
            try
            {
                CaraTmp = ConvertirCaraBD(Cara); //DCF
                if (PropiedadesCara.ContainsKey(CaraTmp))
                {
                    PropiedadesCara[Cara].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(CaraTmp) + "|Evento|Recibe Detencion por Monitoreo de Chip");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEVento_FinalizarVentaPorMonitoreoCHIP: " + Excepcion);
                SWRegistro.Flush();
            }

        }

        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)
        {
            try
            {

                try
                {
                    //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
                    for (int i = 1; i <= mangueras.Count; i++)
                    {
                        Manguera OManguera = mangueras.get_Item(i);

                        foreach (RedSurtidor ORedSurtidor in PropiedadesCara.Values)
                        {
                            foreach (Grados OGrado in ORedSurtidor.ListaGrados)
                            {
                                if (OGrado.MangueraBD == OManguera.idManguera)
                                {
                                    ORedSurtidor.CambiarProductoAMangueras = true;
                                    OGrado.IdProductoACambiar = OManguera.IdProductoActivo;
                                    OGrado.PrecioNivel1 = Convert.ToDecimal(OManguera.Precio);
                                    OGrado.PrecioNivel2 = Convert.ToDecimal(OManguera.Precio);
                                    OGrado.CambiarProducto = true;
                                    SWRegistro.WriteLine(DateTime.Now + "|" + ORedSurtidor.CaraBD +
                                        "|Evento| Recibe evento para Cambio Precio Kardex. Manguera: " + OGrado.MangueraBD +
                                        " - Producto: " + OGrado.IdProducto + " - Solicitud de cambio de producto");
                                    SWRegistro.Flush();
                                }
                            }
                        }
                    }
                }
                catch (Exception Excepcion)
                {
                    string MensajeExcepcion = "Excepcion en el Evento oEvento_ProgramarCambioPrecioKardex: " + Excepcion;
                    SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_ProgramarCambioPrecioKardex:" + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto) { }

        public void Evento_FinalizarCambioTarjeta(byte Cara) { }

        public void Evento_FinalizarVentaPorMonitoreoCHIP(byte Cara) { }

        public void Evento_CerrarProtocolo() { }

        private void oEvento_CerrarProtocolo()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Recibe evento de detencion de Protocolo");
                SWRegistro.Flush();
                CondicionCiclo = false;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|oEventoCerrarProtocolo: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_CancelarVenta(byte Cara)
        {

        }

        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
        }


        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
        {

            try
            {

                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (Solicitar Lecturas por Surtidor). Surtidores: " + Surtidor);
                SWRegistro.Flush();


                CondicionCiclo2 = false; //detiene las encuenstas DCF 

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso1: CondicionCiclo2 = " + CondicionCiclo2);
                SWRegistro.Flush();

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara      
                byte CaraLectura;

                if (!string.IsNullOrEmpty(Surtidor))
                {
                    //Organiza banderas de pedido de lecturas para la cara IMPAR
                    CaraLectura = Convert.ToByte(Convert.ToInt16(Surtidor) * 2 - 1);

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura|...... " + CaraLectura);
                    SWRegistro.Flush();


                    //Si la cara esta activa se solicita la toma de lecturas en la apertura
                    if (PropiedadesCara[CaraLectura].Activa)//DCF19/01/2018
                    {

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {

                            if (PropiedadesCara[CaraTmp].Estado == EstadoCara.WayneReposo)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {

                                CaraEncuestada = CaraTmp;
                                CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias
                               

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();
                                while (!EncuentaFinalizada)
                                {
                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Esperando fin de encuesta");
                                    //SWRegistro.Flush();
                                    System.Threading.Thread.Sleep(100);

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))   
                                }

                                //TomarLecturas(); // obtener las lecturas de la cara en cuestion 

                                foreach (Grados Grado in PropiedadesCara[CaraTmp].ListaGrados) //Recorre las mangueras de la cara encuestada //DCF 06/07/2018
                                {
                                    PropiedadesCara[CaraTmp].GradoCara = Grado.NoGrado; //DCF para tomar los totalizadores de todas las mangueras 

                                    if (ProcesoTomaLectura())
                                    {
                                        //int i;
                                        //for (i = 0; i <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; i++)
                                        //{
                                            Lecturas += (Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                                            Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].Lectura) + "|");

                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                                PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                                                PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].Lectura);
                                            SWRegistro.Flush();
                                        //}
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Error| No se tomaron las lecturas");
                                        SWRegistro.Flush();

                                    }
                                }

                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Inconsistencia|Cara No esta en Reposo. Estado: " + PropiedadesCara[CaraTmp].Estado);
                                SWRegistro.Flush();

                                Lecturas = "E_ Manguera levantada";
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: SolicitarLecturasSurtidor");
                            SWRegistro.Flush();

                            Lecturas = "E_ Cara fuera de red";
                        }

                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|Cara No Activa");//DCF19/01/2018
                        SWRegistro.Flush();
                    }


                    //Organiza banderas de pedido de lecturas para la cara PAR
                    CaraLectura = Convert.ToByte(Convert.ToInt16(Surtidor) * 2);

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura|...... " + CaraLectura);
                    SWRegistro.Flush();

                    //Si la cara esta activa se solicita la toma de lecturas en la apertura
                    if (PropiedadesCara[CaraLectura].Activa)//DCF19/01/2018
                    {
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            if (PropiedadesCara[CaraTmp].Estado == EstadoCara.WayneReposo)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {

                                CaraEncuestada = CaraTmp;
                                CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                                //Para manejar la cara 16 Fecha: 20130821
                                if (CaraEncuestada == 16)
                                {
                                    CaraEncuestada = 0;
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();

                                while (!EncuentaFinalizada)
                                {
                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Esperando fin de encuesta");
                                    //SWRegistro.Flush();
                                    System.Threading.Thread.Sleep(100);

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                }

                                //TomarLecturas(); // obtener las lecturas de la cara en cuestion 
                                foreach (Grados Grado in PropiedadesCara[CaraTmp].ListaGrados) //Recorre las mangueras de la cara encuestada //DCF 06/07/2018
                                {
                                    PropiedadesCara[CaraTmp].GradoCara = Grado.NoGrado; //DCF para tomar los totalizadores de todas las mangueras 

                                    if (ProcesoTomaLectura())
                                    {
                                        //int i;
                                        //for (i = 0; i <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; i++)
                                        //{
                                            Lecturas += (Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                                            Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].Lectura) + "|");

                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                                PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                                                PropiedadesCara[CaraTmp].ListaGrados[Grado.NoGrado].Lectura);
                                            SWRegistro.Flush();
                                        //}
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Error| No se tomaron las lecturas 2");
                                        SWRegistro.Flush();

                                    }
                                }

                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Inconsistencia|Cara No esta en Reposo. Estado: " + PropiedadesCara[CaraTmp].Estado);
                                SWRegistro.Flush();

                                Lecturas = "E_ Manguera levantadas";
                            }

                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: SolicitarLecturasSurtidor");
                            SWRegistro.Flush();

                            Lecturas = "E_ Cara fuera de red";
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|Cara No Activa");//DCF19/01/2018
                        SWRegistro.Flush();
                    }

                }



                CondicionCiclo2 = true; //Activa las encuenstas DCF 

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso: CondicionCiclo2 = " + CondicionCiclo2);
                SWRegistro.Flush();

            }
            catch (Exception Excepcion)
            {


                CondicionCiclo2 = true; //Activa las encuenstas DCF 


                string MensajeExcepcion = "Excepcion en el Metodo Lectura_Surtidor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }



        #endregion
    }
}
