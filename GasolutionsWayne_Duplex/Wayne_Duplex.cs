
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

//namespace gasolutions.Protocolos.Wayne_Duplex
namespace POSstation.Protocolos
{

    public class Wayne_Duplex : iProtocolo
    {

        #region EventosDeProtocolo

        public bool AplicaServicioWindows = true;
        public bool AplicaServicioTramas = true;
        public event iProtocolo.CambioMangueraEnVentaGerenciadaEventHandler CambioMangueraEnVentaGerenciada;//

        public event iProtocolo.CaraEnReposoEventHandler CaraEnReposo;//--

        public event iProtocolo.VentaFinalizadaEventHandler VentaFinalizada;//--

        public event iProtocolo.LecturaTurnoCerradoEventHandler LecturaTurnoCerrado;//--

        public event iProtocolo.LecturaTurnoAbiertoEventHandler LecturaTurnoAbierto;//--

        public event iProtocolo.LecturaInicialVentaEventHandler LecturaInicialVenta;//--

        public event iProtocolo.VentaParcialEventHandler VentaParcial;//

        public event iProtocolo.CambioPrecioFallidoEventHandler CambioPrecioFallido;//

        public event iProtocolo.CancelarProcesarTurnoEventHandler CancelarProcesarTurno;//--

        public event iProtocolo.ExcepcionOcurridaEventHandler ExcepcionOcurrida;//--

        public event iProtocolo.VentaInterrumpidaEnCeroEventHandler VentaInterrumpidaEnCero;//

        public event iProtocolo.AutorizacionRequeridaEventHandler AutorizacionRequerida;//--

        public event iProtocolo.IniciarCambioTarjetaEventHandler IniciarCambioTarjeta;//

        public event iProtocolo.LecturasCambioTarjetaEventHandler LecturasCambioTarjeta;//

        public event iProtocolo.NotificarCambioPrecioMangueraEventHandler NotificarCambioPrecioManguera;//

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
            ObtenerTotalizadorImporte_I,
            ObtenerTotalizadorImporte_II,

            FinVenta_AF,
            OffEstado_AF,
            contadorError,

        }   //Define los COMANDOS que se envian al Surtidor

        ComandoSurtidor ComandoCaras;

        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
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

        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        string Puerto;

        AsyncCallback callBack = new AsyncCallback(CallBackMethod);
        TcpClient ClienteWayne;
        NetworkStream Stream;
        int Bytes_leidos;

        bool CondicionCiclo2 = true;
        bool EncuentaFinalizada = false;


        #endregion

        #region PUNTO DE ARRANQUE
        //PUNTO DE ARRANQUE DE LA CLASE
        public Wayne_Duplex(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {


                this.Puerto = Puerto; 

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Wayne-Tramas(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "- Wayne-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.05.13-1613");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.08.09-1100"); //Alias DCF //Dimensión de Archivos de logueo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.11.29-2000"); //Precio Peru
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.01.18-1403"); //tiempos
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2011.01.18-1403"); //tiempos
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_V390 2011.02.14-1538"); //Comados Estado DIfiere
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.02.25-1130"); //Calculo de fin de ventas con el importe Lima Peru, estado AF.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.03.01-1230"); //mas Tiempo de espera.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.03.13-1448"); //LI = LF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.03.17-1453"); //Control de Autorizacion con Predeterminacion e impresion de advertencia.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.03.18-0917"); //Log de estado de Impresion en Autorizacion con Predeterminacion.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.04.07-1712"); //ERRO DE ESTADOS DEL SURTIDOR, GRADO VENDIENDO- GRADO DESCOLGADO DCF 07-04-2011
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.06.12-0930"); //Toma de Total Volumen al iniciar el autorizador y control de volumen inicial = 0 después de reiniciar el autorizador, Grado que vendió difiere al autorizado.  25/06/2011 No funciono
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 2011.07.08-1055"); //Toma de Total Volumen al iniciar el autorizador y control de volumen inicial = 0 después de reiniciar el autorizador,
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 01.06.2012-1140"); //// Si no entrega Lecturas finales los cálculos serán negativos y son errados se envía venta en cero. para 01/06/2012 --1140 DCF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 25.06.2012-1408"); // Alias
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 10.07.2012-1649");  //dcf 10/07/2012 pruebas con simulador funciona. Factor Importe
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 19.08.2012-1850");  //Activa caras log
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 24.10.2012-1733");//24/10/2012 17:33 Se obtiene el totalizador Inicial de importe para que no se tenga problemas en los calculos de importe en el fin de venta  LF - *LI
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 28.11.2013-1107"); //Environment.CurrentDirectory  por  Application.StartupPath
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_Duplex 02.10.2014-1117"); // if (Bytes >= BytesEsperados)  // imeOut = 200;//400;
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.08.23-1550");//DCF log factores 23/08/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.12.18-1155");// DCF PuertoCom.BaudRate = 4800; //9600; Cali Barrio Nuevo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.12.27-1510");//Thread.Sleep(2000); //tiempo de espera para la predeterminacion desde el cabezal DCF 15-03-11
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.03.08-1745");//DCF Archivos .txt 08/03/2018  
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP_Duplex 2018.07.09- 1040 #");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF 09/07/2018
                SWRegistro.Flush();

                ////Instancia los eventos disparados por la aplicacion cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //Evento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //Evento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //Evento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //Evento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                ////Evento.ProgramarCambioPrecioKardex += new SharedEventsFuelStation.__CMensaje_ProgramarCambioPrecioKardexEventHandler(Evento_ProgramarCambioPrecioKardex);
                //Evento.FinalizarVentaPorMonitoreoCHIP += new SharedEventsFuelStation.__CMensaje_FinalizarVentaPorMonitoreoCHIPEventHandler(Evento_FinalizarVentaPorMonitoreoCHIP);
                //Evento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(Evento_CerrarProtocolo);

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 4800; //9600;
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

                /*  foreach (RedSurtidor oCara in PropiedadesCara.Values)
                  {
                      foreach (Grados oGrado in PropiedadesCara[oCara.Cara].ListaGrados)
                      {
                          SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                              " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " - Venta Parcial: " + oCara.EsVentaParcial);                       

                      }
                  }
                  SWRegistro.Flush();
                  */
                //Variable que determina si la interfaz física de los surtidores añade eco a las tramas recibida
                TramaEco = Eco;

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


        public Wayne_Duplex(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
                this.AplicaServicioWindows = true;
                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Wayne-Tramas(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "- Wayne-Sucesos(" + Puerto + ").txt";
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
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2014.10.02-1832"); //TCP/IP time estado 400
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.08.23-1549");//DCF log factores 23/08/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.12.18-1155");// DCF PuertoCom.BaudRate = 4800; //9600; Cali Barrio Nuevo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2017.12.27-1510");//Thread.Sleep(2000); //tiempo de espera para la predeterminacion desde el cabezal DCF 15-03-11
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP 2018.03.08-1745");//DCF Archivos .txt 08/03/2018  
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne_TCP/IP_Duplex 2018.07.09- 1220 *");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF 09/07/2018
                SWRegistro.Flush();
                //Instancia los eventos disparados por la aplicacion cliente



                //PropiedadesCara es la referencia con la que se va a trabajar
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                foreach (RedSurtidor oCara in PropiedadesCara.Values)
                {
                    foreach (Grados oGrado in PropiedadesCara[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " - Venta Parcial: " + oCara.EsVentaParcial);
                }
                SWRegistro.Flush();

                //Variable que determina si la interfaz física de los surtidores añade eco a las tramas recibida
                TramaEco = Eco;

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


                //*************************************************************** DCF 14_03_11
                // se utiliza para sacar de estado preset
                foreach (RedSurtidor ORedCaras in PropiedadesCara.Values)
                {

                    if (ORedCaras.Activa == true)
                    {
                        CaraEncuestada = ORedCaras.Cara;
                        //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria

                        CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                        ProcesoEnvioComando(ComandoSurtidor.Estado);

                        if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WaynePredeterminada)
                        {
                            ProcesoEnvioComando(ComandoSurtidor.OffEstado_AF);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Reset Estado WaynePredeterminada al Iniciar Protocolo");
                            SWRegistro.Flush();
                        }

                        Thread.Sleep(20);
                    }


                   
                }


                // ********************** ******************** ***********************
                // ********************** ******************** ***********************
                //Tomar los totalizadores al inicial el autorizador 

                foreach (RedSurtidor oCara in PropiedadesCara.Values)
                {

                    if (oCara.Activa == true)
                    {

                        foreach (Grados oGrado in PropiedadesCara[oCara.Cara].ListaGrados)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                                " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " - Venta Parcial: " + oCara.EsVentaParcial);
                            SWRegistro.Flush();
                            //PropiedadesCara[CaraEncuestada].GradoCara = oGrado.IdProducto; Error
                            PropiedadesCara[CaraEncuestada].GradoCara = oGrado.NoGrado;
                            CaraEncuestada = oCara.Cara; //Cara 

                            ProcesoTomaLectura();


                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaInicialVenta =
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura;



                            ProcesoTomaLecturaImporte();//24/10/2012 17:33 Se obtiene el totalizador Inicial de importe para que no se tenga problemas en los calculos de importe en el fin de venta  LF - *LI

                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaInicialImporte =
                                 PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte;


                        }
                    }


                }

                //para loguear los factores        //DCF log factores 23/08/2017
                foreach (RedSurtidor ORedCaras2 in PropiedadesCara.Values)
                {
                    byte CaraEncuestada2 = ORedCaras2.Cara;

                    //if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)-- 09/05/2012
                    if (PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta == 0)
                    {
                        PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta = 1;
                    }

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada2 + "|FactorVolumen: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorVolumen)
                           + " - FactorTotalizador: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorTotalizador)
                           + " - FactorImporte: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorImporte)
                           + " - FactorPrecio: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorPrecio)
                           + " - MultiplicadorPrecioVenta: " + PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta
                           + " - Wayne_Extended: " + PropiedadesCara[CaraEncuestada2].Gilbarco_Extended);              
                  
                    SWRegistro.Flush();
                }


                // ********************** ******************** ***********************
                // ********************** ******************** ***********************


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

                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                if (ProcesoEnvioComando(ComandoSurtidor.Estado))
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

        public void VerifySizeFile() //Analiza el tamaño del archivo 
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Wayne-Tramas(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "- Wayne-Sucesos(" + Puerto + ").txt";
                    SWRegistro = File.AppendText(Archivo);
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|VerifySizeFile: " + Excepcion);
                SWRegistro.Flush();
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
                int MaximoReintento = 6;// antes 2 DCF

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

                //Arma la trama de Transmision
                ArmarTramaTx();

                do
                {
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

                            if (AplicaServicioWindows)
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

                            if (AplicaServicioWindows)
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
                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseI();
                        TramaTx = new byte[5] { 0x00, 0x00, CaraWayne, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.Autorizar:
                        TimeOut = 300;
                        CaraWayne = AsignacionCaraClaseII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x8F, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.ObtenerPrecio:
                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x00, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;


                    case ComandoSurtidor.EstalecerPrecio:
                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseIII();

                        string strPrecioHex = Convert.ToInt32(
                            (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1
                            * PropiedadesCara[CaraEncuestada].FactorPrecio)).ToString("X2").PadLeft(4, '0');

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


                    //case ComandoSurtidor.ObtenerVentaDinero: Wayne duplex no maneja este comando
                    //    TimeOut = 200;
                    //    CaraWayne = AsignacionCaraClaseIII();
                    //    TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                    //    break;

                    //case ComandoSurtidor.ObtenerVentaVolumen: Wayne duplex no maneja este comando
                    //    TimeOut = 200;
                    //    CaraWayne = AsignacionCaraClaseIII();
                    //    TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x06, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                    //    break;





                    //*************************************
                    //*************************************                       
                    //Obtener Importe
                    //*************************************
                    //*************************************

                    case ComandoSurtidor.ObtenerTotalizadorImporte_I:
                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x02, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;

                    case ComandoSurtidor.ObtenerTotalizadorImporte_II:
                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x04, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        break;



                    //*************************************
                    //*************************************                       
                    //Obtener Volumen
                    //*************************************
                    //*************************************

                    case ComandoSurtidor.ObtenerTotalizador_I:

                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x02, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara + 0x30), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };

                        break;


                    case ComandoSurtidor.ObtenerTotalizador_II:

                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x04, 0x00, (byte)(PropiedadesCara[CaraEncuestada].GradoCara + 0x30), 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };

                        break;





                    case ComandoSurtidor.Predeterminar: //Wayne no responde a este comando
                        TimeOut = 200;
                        CaraWayne = AsignacionCaraClaseIII();
                        string strPreset =
                            Convert.ToInt64(PropiedadesCara[CaraEncuestada].ValorPredeterminado * PropiedadesCara[CaraEncuestada].FactorImporte).ToString().PadLeft(6, '0');
                        byte PresetA = Convert.ToByte(strPreset.Substring(strPreset.Length - 6, 2), 16);
                        byte PresetM = Convert.ToByte(strPreset.Substring(strPreset.Length - 4, 2), 16);
                        byte PresetB = Convert.ToByte(strPreset.Substring(strPreset.Length - 2, 2), 16);
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x21, 0x00, PresetB, 0x00, PresetM, 0x00, PresetA, 0x00, 0xFF };
                        break;


                    //case ComandoSurtidor.FinVenta_AF:
                    //    TimeOut = 200;
                    //    CaraWayne = AsignacionCaraClaseII();
                    //     TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };                  

                    //    break;I

                    case ComandoSurtidor.OffEstado_AF: //Cancelar Preset
                        TimeOut = 400;
                        CaraWayne = AsignacionCaraClaseII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x87, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF }; //del Pec ok 

                        //CaraWayne = AsignacionCaraClaseIII();
                        //TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x06, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        //TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };  
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
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + " ComandoCaras: " + ComandoCaras + "|Excepcion|ArmarTramaTx: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        private byte

            AsignacionCaraClaseI() //Codigo de cara Encuestada para Comando ESTADO 
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
                ///////////////////////////////////////////////////////////////////////////////////

                //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
                //eco = Convert.ToByte(TramaTx.Length); //respuesta del LOOP de Corriente

                //Tiempo muerto mientras el Surtidor Responde
                //Thread.Sleep(TimeOut + 3000); //DCF Borra solo para testeo
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

                    eco = Convert.ToByte(TramaTx.Length); //respuesta del LOOP de Corriente
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
                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + ComandoCaras);
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

                if (!TramaEco)
                    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                int BytesEsperados = 0x0D + eco;

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                if (Bytes >= BytesEsperados)
                //if (Bytes > 0) //Para prueba observacion
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
                if (!TramaEco)
                    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                int BytesEsperados = 0x0D + eco;

                byte[] TramaRxTemporal = new byte[BytesEsperados];

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
                    }


                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    if (Bytes_leidos >= BytesEsperados)
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


        public void LimpiarSockets()
        {
            try
            {
                //ClienteGilbarco.Client.Disconnect(false);  
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



        //REVISA LA INTEGRIDAD DE LA TRAMA
        private bool ComprobarIntegridadTrama()
        {
            try
            {

                if (TramaRx[0] == 0x00 && TramaRx[1] == 0x00) //Inicio de Trama 0x00 0x00
                {
                    if (TramaRx[12] == 0xFF) //Fin de Trama 0x0D
                    {
                        for (int i = 2; i <= 10; i += 2)
                        {
                            if (TramaRx[i + 1] != 0xFF - TramaRx[i])
                                return false;
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
                        //  SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|FactorPrecio: " + Convert.ToDecimal(PropiedadesCara[CaraEncuestada].FactorPrecio));
                        //SWRegistro.Flush(); //DCF Borra

                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 =
                            Convert.ToDecimal(Convert.ToInt32(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2"), 16)) /
                            Convert.ToDecimal(PropiedadesCara[CaraEncuestada].FactorPrecio);



                        break;

                    case ComandoSurtidor.ObtenerVentaDinero:
                        // SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|FactorImporte: " + PropiedadesCara[CaraEncuestada].FactorImporte);
                        //SWRegistro.Flush(); //DCF Borra
                        PropiedadesCara[CaraEncuestada].TotalVenta =
                            Convert.ToDecimal(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2") + TramaRx[6].ToString("X2")) /
                            PropiedadesCara[CaraEncuestada].FactorImporte;
                        break;

                    case ComandoSurtidor.ObtenerVentaVolumen:
                        // SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|FactorVolumen: " + PropiedadesCara[CaraEncuestada].FactorVolumen);
                        //SWRegistro.Flush(); //DCF Borra
                        PropiedadesCara[CaraEncuestada].Volumen =
                           Convert.ToDecimal(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2") + TramaRx[6].ToString("X2")) /
                           PropiedadesCara[CaraEncuestada].FactorVolumen;
                        break;


                    //**************************
                    // Volumen
                    //**************************

                    case ComandoSurtidor.ObtenerTotalizador_I:
                        AuxiliarLectura = TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2");
                        //modificACION PARA EL SURTIDOR QUE NO RESPONDA CORRECTAMENTE AL COMADO TOTALIZADOR CON TIPO MANGUERA 30-31-32:: DEBE SER 00-01-02

                        break;

                    case ComandoSurtidor.ObtenerTotalizador_II:
                        // SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|FactorTotalizador: " + PropiedadesCara[CaraEncuestada].FactorTotalizador);
                        //SWRegistro.Flush(); //DCF Borra
                        AuxiliarLectura = AuxiliarLectura + TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2");
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura =
                            Convert.ToDecimal(AuxiliarLectura) / PropiedadesCara[CaraEncuestada].FactorTotalizador;
                        break;



                    //**************************
                    //Importe
                    //**************************

                    case ComandoSurtidor.ObtenerTotalizadorImporte_I:
                        AuxiliarLectura = TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2");
                        break;

                    case ComandoSurtidor.ObtenerTotalizadorImporte_II:
                        //AuxiliarLectura = AuxiliarLectura + TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2");
                        //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte =
                        //    Convert.ToDecimal(AuxiliarLectura) / PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte;

                        AuxiliarLectura = AuxiliarLectura + TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2"); //dcf 10/07/2012 pruebas con simulador funciona. Factor Importe
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte =
                            Convert.ToDecimal(AuxiliarLectura) / PropiedadesCara[CaraEncuestada].FactorImporte;





                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Importe: " + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara               [CaraEncuestada].GradoCara].LecturaImporte);
                        //SWRegistro.Flush(); //DCF Borra
                        break;


                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|AnalizarTrama: " + " | ComandoCaras :" + ComandoCaras + " | " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void RecuperarEstado()
        {
            try
            {
                /*Estados:
                 * 
                 // Electronica iGEM V3-4
                -	07 Mangueras colgadas
                -	00 Manguera 1 descolgada
                -	01 Manguera 2 descolgada
                -	02 Manguera 3 descolgada
                -	03 Manguera 4 descolgada
                 * 
                 *                  
                //Electronica Duplex V2 con sw en manual //DCF
                -	27 Mangueras colgadas
                -	20 Manguera 1 descolgada
                -	21 Manguera 2 descolgada
                -	22 Manguera 3 descolgada
                -	23 Manguera 4 descolgada
                 * 
                 * 
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


                //switch (Convert.ToInt16(TramaRx[10]) & (0x0f))
                //{
                //    case (0x07)://iGEM
                //    //case (0x27)://DCF Duplex   

                //        if (PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.WayneDespacho ||
                //            PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                //            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneFinDespachoForzado;
                //        else
                //            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneReposo;
                //        break;
                //}
                

                //Asigna Estado
                //switch (TramaRx[10])
          
                switch (TramaRx[10])
                {

                    case (0xA0)://Manguera 1 Descolgada//iGEM
                    case (0xA1)://Manguera 2 Descolgada//iGEM
                    case (0xA2)://Manguera 3 Descolgada//iGEM
                    case (0xA3)://Manguera 4 Descolgada//iGEM
                    case (0x00)://Manguera 1 Descolgada//iGEM
                    case (0x01)://Manguera 2 Descolgada//iGEM
                    case (0x02)://Manguera 3 Descolgada//iGEM
                    case (0x03)://Manguera 4 Descolgada//iGEM
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDescolgada;
                        //Obtener Grado de la cara
                        PropiedadesCara[CaraEncuestada].GradoVenta = Convert.ToInt16((TramaRx[10]) & (0x0f)); //(TramaRx[10]);
                        //PropiedadesCara[CaraEncuestada].GradoVentaInicial = (TramaRx[10]); //DCF 12-03-11
                       break;

                    case (0x07)://iGEM
                    case (0xA7):
                    case (0x27)://DCF Duplex                   
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.WayneDespacho ||
                            PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneFinDespachoForzado;
                        else
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneReposo;
                        break;

                    //Para surtidores V2 con electronica Duplex DCF
                    case (0x20)://Manguera 1 Descolgada// Duplex
                    case (0x21)://Manguera 2 Descolgada// Duplex
                    case (0x22)://Manguera 3 Descolgada// Duplex
                    case (0x23)://Manguera 4 Descolgada// Duplex
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDescolgada;
                        //Obtener Grado de la cara
                        PropiedadesCara[CaraEncuestada].GradoVenta = (TramaRx[10] - 0x20); //DCF
                        //PropiedadesCara[CaraEncuestada].GradoVentaInicial = (TramaRx[10] - 0x20); //DCF
                        break;

                    case (0x88):
                    case (0xA8)://Manguera 1 Despachando
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                        PropiedadesCara[CaraEncuestada].GradoVenta = 0;
                        break;


                    case (0x89):
                    case (0xA9)://Manguera 2 Despachando
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                        PropiedadesCara[CaraEncuestada].GradoVenta = 1;
                        break;


                    case (0x8A):
                    case (0xAA)://Manguera 3 Despachando
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                        PropiedadesCara[CaraEncuestada].GradoVenta = 2;
                        break;

                    case (0x8B):
                    case (0xAB)://Manguera 4 Despachando
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespacho;
                        PropiedadesCara[CaraEncuestada].GradoVenta = 3;
                        break;

                    case (0x8F):
                    case (0xAF)://termino la Carga
                        //PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneFinDespacho;
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneFinDespacho_AF;
                        break;

                    case (0xF1)://Autorizado listo para la vender
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneDespachoAutorizado;
                        break;

                    case (0xF5)://Autorizado listo para la vender
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WayneBloqueada;
                        break;

                    case (0x0F):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.WaynePredeterminada;
                        break;

                    default:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Estado Indeterminado: " + TramaRx[10].ToString("X2"));
                        SWRegistro.Flush();
                        break;
                
                }



                //Parciales de venta se recuperan en el comando estado
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

                            if (AplicaServicioWindows)
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
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            LecturaAperturaCierre();

                        PropiedadesCara[CaraEncuestada].ContadorError = 0; //Reset de erro al predeterminar DCF

                        break;


                    #region EstadoCara.WayneDescolgada
                    case (EstadoCara.WayneDescolgada):
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Manguera descolgada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                if (AplicaServicioWindows)
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

                                if (AplicaServicioWindows)
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
                            PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta;
                            if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                            {
                                if (ProcesoTomaLectura()) // Toma de totalizadores volumen
                                {
                                    int IdProducto =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto;
                                    int IdManguera =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;
                                    string Lectura =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura.ToString("N3");


                                    if (AplicaServicioWindows)
                                    {
                                        if (AutorizacionRequerida != null)
                                        {
                                            AutorizacionRequerida(CaraID, IdProducto, IdManguera, Lectura,"");
                                        }
                                    }
                                    PropiedadesCara[CaraEncuestada].GradoVentaInicial = PropiedadesCara[CaraEncuestada].GradoVenta; //DCF 25/06/2011 Que grado fue autorizado Grado inicial

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


                                if (ProcesoTomaLecturaImporte()) //Toma de Totalizadores de importe
                                {
                                    string lecturaImporte =
                                             PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte.ToString("N3");

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
                            //  Evento.InformarLecturaInicialVenta( CaraID,  strLecturasVolumen); //Se deve enviar el importe ???? DCF 

                            if (AplicaServicioWindows)
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

                            //lectura inicial de importe para el calculo final de la venta DCF
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte;

                            string strLecturasImporte =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte.ToString("N3");

                            //Loguea Evento de envio de lectura
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Lectura Inicial de Importe: " +
                                strLecturasImporte);
                            SWRegistro.Flush();
                            //*********************************** DCF


                            if (PropiedadesCara[CaraEncuestada].PredeterminarVolumen)
                            {
                                PropiedadesCara[CaraEncuestada].ValorPredeterminado = PropiedadesCara[CaraEncuestada].ValorPredeterminado
                                    * PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioNivel1;

                                PropiedadesCara[CaraEncuestada].PredeterminarImporte = true;
                                PropiedadesCara[CaraEncuestada].PredeterminarVolumen = false;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Predeterminacion Volumen.  Conversion a dinero: " +
                                        PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                                SWRegistro.Flush();
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



                            //Thread.Sleep(2000); //tiempo de espera para la predeterminacion desde el cabezal DCF 15-03-11

                            ProcesoEnvioComando(ComandoSurtidor.Estado); // se envia consultar estado para mirar si esta predeterminada DCF 15-03-11

                            if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WaynePredeterminada &&
                                PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneDescolgada) //DCF 15-03-11
                            {

                                int Reintenos = 1;
                                do
                                {
                                    if (!ProcesoEnvioComando(ComandoSurtidor.Autorizar)) //Autorizacion
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondió comando de Autorizar Despacho");
                                        SWRegistro.Flush();
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Comando Autorizacion enviado con exito");
                                        SWRegistro.Flush();
                                    }

                                    ProcesoEnvioComando(ComandoSurtidor.Estado);
                                    Reintenos++;
                                } while (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespacho &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespachoAutorizado && Reintenos <= 2 &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WaynePredeterminada &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneFinDespacho_AF &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneFinDespacho &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneFinDespachoForzado); //DCF WaynePredeterminada 15-03-11 WayneFinDespacho_AF
                            }


                            //DCF 15-03-11 ********************************************
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WaynePredeterminada) // envio de mensaje si se predetermina con manguera levantada 15-03-11
                            {
                                string caraError = Convert.ToString(CaraEncuestada);

                                string Mensaje = "Error al Predeterminar. Por Favor Cuelgue la Manguera, predetermine el valor antes de levantar la manguera para realizar la venta satisfactoriamente en la cara: ";
                                Mensaje = Mensaje + caraError;
                                bool Imprime = true;
                                bool Terminal = false;
                                string puerto = "COM1";

                                // Evento.ReportarExcepcion(  Mensaje,   Imprime,   Terminal,   puerto);

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| Impresion de Error al Predeterminar con Manguera Descolgada");
                                SWRegistro.Flush();

                            }
                            //DCF 15-03-11 ********************************************





                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneDespachoAutorizado ||
                                PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneDespacho)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Comando Autorizacion Aceptado");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = true;

                                //// grabar el grado que inicio la venta DCF 04-07-11
                                //PropiedadesCara[CaraEncuestada].GradoVentaInicial = PropiedadesCara[CaraEncuestada].GradoVenta; //25/06/2011



                            }
                            else if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WayneReposo)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Manguera colgada luego de autorizada");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false;

                                if (AplicaServicioWindows)
                                {
                                    if (VentaInterrumpidaEnCero != null)
                                    {
                                        VentaInterrumpidaEnCero(CaraID);
                                    }
                                }
                            }
                            else if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespachoAutorizado &&
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneDespacho) //corigir el estado Waynepredeterminada DCF
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Comando Autorizacion No aceptado");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                            }

                            //Activar Bandera para precaucion en fin de Venta DCF 16-03-11 ********************************************
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WaynePredeterminada && PropiedadesCara[CaraEncuestada].AutorizarCara == false)
                            {
                                PropiedadesCara[CaraEncuestada].PosibleErrorFinVenta = true;
                            }
                            else
                            {
                                PropiedadesCara[CaraEncuestada].PosibleErrorFinVenta = false;
                            }
                            //****************************************************************************************



                        }
                        break;

                    #endregion;

                    case EstadoCara.WayneDespachoAutorizado:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara Autorizada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                if (AplicaServicioWindows)
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

                                if (AplicaServicioWindows)
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




                    //case EstadoCara.WayneFinDespacho_AF:  //Desbloquear el estado 0xAF:

                    //    if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.WayneFinDespacho_AF)
                    //    {
                    //        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado Anterior: " + PropiedadesCara[CaraEncuestada].EstadoAnterior + "|Estado Anterior:" + PropiedadesCara[CaraEncuestada].Estado); //DCF Borrar
                    //        SWRegistro.Flush();

                    //        ProcesoEnvioComando(ComandoSurtidor.OffEstado_AF);
                    //    }

                    //    break;


                    case EstadoCara.WayneFinDespacho_AF:
                    case EstadoCara.WayneFinDespachoForzado:


                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.WayneFinDespacho_AF)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado Anterior: " + PropiedadesCara[CaraEncuestada].EstadoAnterior + "|Estado :" + PropiedadesCara[CaraEncuestada].Estado); //DCF Borrar
                            SWRegistro.Flush();

                            //ProcesoEnvioComando(ComandoSurtidor.OffEstado_AF);
                            //Thread.Sleep(200);
                            //ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio);

                            ProcesoEnvioComando(ComandoSurtidor.OffEstado_AF);
                            //Thread.Sleep(200);
                            //ProcesoEnvioComando(ComandoSurtidor.OffEstado_AF1);
                        }


                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en fin de despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                if (AplicaServicioWindows)
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

                                if (AplicaServicioWindows)
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

                                if (AplicaServicioWindows)
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

                                if (AplicaServicioWindows)
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

                        if (AplicaServicioWindows)
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

                                if (AplicaServicioWindows)
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

                                if (AplicaServicioWindows)
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


                    case EstadoCara.WaynePredeterminada: //DCF 15-03-11

                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.WaynePredeterminada)
                        {
                            PropiedadesCara[CaraEncuestada].ContadorError += 1;

                            if (PropiedadesCara[CaraEncuestada].ContadorError == 600) // Aproximadamente 20 minutos para enviar comando de cancelacion de preset.
                            {

                                string caraError = Convert.ToString(CaraEncuestada);

                                string Mensaje = "Error al Predeterminar, Tiempo Max. Por Favor Cuelgue la Manguera, predetermine el valor antes de levantar la manguera para realizar la venta satisfactoriamente en la cara: ";
                                Mensaje = Mensaje + caraError;
                                bool Imprime = true;
                                bool Terminal = false;
                                string puerto = "COM1";

                                //void ReportarExcepcion( string Mensaje,  bool Imprime,  bool Terminal,  string puerto = "");
                                // Evento.ReportarExcepcion(  Mensaje,   Imprime,   Terminal,   puerto);

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| Impresión de Error en predeterminación");
                                SWRegistro.Flush();

                                if (ProcesoEnvioComando(ComandoSurtidor.OffEstado_AF)) //Reset de erro al predeterminar 
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + " |Reset del Estado WaynePredeterminada Tiempo Max Surtidor Bloqueado.");
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + " |No acepto el Reset del Estado WaynePredeterminada");
                                    SWRegistro.Flush();
                                }


                                ProcesoEnvioComando(ComandoSurtidor.Estado); //Consulto el estado para verificar que si se resetio la cara bloqueada

                                if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WaynePredeterminada)
                                {

                                    caraError = Convert.ToString(CaraEncuestada);

                                    Mensaje = "Error al Predeterminar, Surtidor No acepto comando de desbloque. Por Favor Reinicie el surtidor.  Cara: ";
                                    Mensaje = Mensaje + caraError;
                                    Imprime = true;
                                    Terminal = false;
                                    puerto = "COM1";

                                    //Evento.ReportarExcepcion(  Mensaje,   Imprime,   Terminal,   puerto);

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + " |Surtidor No acepto comando de desbloque. Por Favor Reinicie el surtidor.");
                                    SWRegistro.Flush();

                                }

                                PropiedadesCara[CaraEncuestada].ContadorError = 0; //Reset de erro al predeterminar 

                            }

                        }


                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado Cara|" + PropiedadesCara[CaraEncuestada].Estado.ToString());
                        SWRegistro.Flush();


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



        private bool ProcesoTomaLectura()
        {
            try
            {

                //ProcesoTomaLecturaImporte(); // Obtiene el totalizador de importe "ventas realizadas dinero "

                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador_I))
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador_II))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Toma de Totalizador Vol Exitoso: " +
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



        private bool ProcesoTomaLecturaImporte()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizadorImporte_I))
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizadorImporte_II))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Toma de lecturas Importe Exitoso: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte);
                        SWRegistro.Flush();
                        return true;
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener ObtenerTotalizadorImporte_II");
                        SWRegistro.Flush();
                        return false;
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener ObtenerTotalizadorImporte_I");
                    SWRegistro.Flush();
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ObtenerTotalizadorImporte: " + Excepcion);
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

                PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta;


                // //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                //if (PropiedadesCara[CaraEncuestada].GradoCara!= PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 20110117
                //{
                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inconsistencia|Grado autorizado: " + PropiedadesCara[CaraEncuestada].GradoCara +
                //        " - Grado que vendio: " + PropiedadesCara[CaraEncuestada].GradoVenta);
                //    SWRegistro.Flush();
                //}


                //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 07-04-2011
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado autorizado: " + PropiedadesCara[CaraEncuestada].GradoVentaInicial +
                        " - Grado que vendio: " + PropiedadesCara[CaraEncuestada].GradoVenta);
                    SWRegistro.Flush();
                }

                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                {
                    PropiedadesCara[CaraEncuestada].PrecioVenta =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1;
                }

                if (!ProcesoTomaLectura()) //Obtener Volumen por diferencias de lecturas en totalizadores volumen
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de totalizadores para Lectura Final de Venta");
                    SWRegistro.Flush();

                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta = 0; //DCF             
                }
                else
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura;



                if (!ProcesoTomaLecturaImporte()) //Obtener Importe por diferencias de lecturas en totalizadores Importe
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de totalizadores para Lectura Final de Importe");
                    SWRegistro.Flush();

                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte = 0; //DCF             
                }
                else
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte;



                // Si no entrega Lecturas finales los cálculos serán negativos y son errados se envía venta en cero. para 01/06/2012 --1140 DCF
                if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta > 0 ||
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte > 0)
                {

                    //*************************************************
                    // se calcula el Importe de la venta realizada DCF
                    //*************************************************
                    //PropiedadesCara[CaraEncuestada].TotalVenta =
                    //(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte -
                    //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte) /
                    //PropiedadesCara[CaraEncuestada].FactorImporte;

                    PropiedadesCara[CaraEncuestada].TotalVenta =
                     (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte -
                     PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte); //dcf 10/07/2012 pruebas con simulador funciona. Factor Importe


                    if (PropiedadesCara[CaraEncuestada].TotalVenta != 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Final de Venta, Importe Calculado: " + PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3"));
                        SWRegistro.Flush();
                    }
                    //*************************************************
                    //*************************************************


                    //*************************************************
                    // Volumen Calculado por Vol = (Importe/P.producto) 
                    //*************************************************  

                    PropiedadesCara[CaraEncuestada].Volumen = ((PropiedadesCara[CaraEncuestada].TotalVenta) / (PropiedadesCara[CaraEncuestada].PrecioVenta));
                    //* PropiedadesCara[CaraEncuestada].FactorVolumen;
                    if (PropiedadesCara[CaraEncuestada].Volumen != 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Final de Venta, Volumen Calculado: " + PropiedadesCara[CaraEncuestada].Volumen.ToString("N3"));
                        SWRegistro.Flush();
                    }
                    //*************************************************
                    //*************************************************



                    if (PropiedadesCara[CaraEncuestada].Volumen <= 0)
                    {
                        //*************************************************
                        // Se calcula el Volumen de la venta realizada DCF no es muy preciso el totalizador no entrega la última cifra
                        //*************************************************     
                        PropiedadesCara[CaraEncuestada].Volumen =
                        (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta -
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta);
                        ///PropiedadesCara[CaraEncuestada].FactorVolumen;

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Final de Venta, Volumen Calculado por Diferencia VF - VI = " + PropiedadesCara[CaraEncuestada].Volumen.ToString("N3"));
                        SWRegistro.Flush();

                        //*************************************************
                        //*************************************************
                    }


                    if (PropiedadesCara[CaraEncuestada].TotalVenta <= 0) //Importe negativo
                    {
                        PropiedadesCara[CaraEncuestada].TotalVenta = (PropiedadesCara[CaraEncuestada].Volumen * (PropiedadesCara[CaraEncuestada].PrecioVenta));
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Final de Venta, Importe Calculado por Diferencia Vol*P.venta =" + PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3"));
                        SWRegistro.Flush();
                    }


                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|El surtidor No entrego Totalizadores finales de venta..");
                    SWRegistro.Flush();
                }

                //Evalúa si la venta viene en 0
                //if (PropiedadesCara[CaraEncuestada].Volumen != 0 || PropiedadesCara[CaraEncuestada].TotalVenta != 0)
                if (PropiedadesCara[CaraEncuestada].Volumen > 0 && PropiedadesCara[CaraEncuestada].TotalVenta > 0)
                {
                    //Almacena los valores en las variables requerida por el Evento
                    string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");//Importe
                    string strPrecio = PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                    string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                    string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");
                    string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                    string strLecturaFinalImporte = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte.ToString("N3"); //DCF enviar a DB
                    string strLecturaInicialImporte = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte.ToString("N3");//DCF enviar a DB

                    string bytProducto = Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto);
                    int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;

                    //Si pudo finalizar correctamente el procceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;

                    //Loguea evento Fin de Venta
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|InformarFinalizacionVenta. Importe: " + strTotalVenta +
                        " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                        " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                    SWRegistro.Flush();

                    String PresionLLenado = "0";
                    //Evento.InformarFinalizacionVenta( CaraID,  strTotalVenta,  strPrecio,  strLecturaFinalVenta,
                    //           strVolumen,  bytProducto,  IdManguera,  PresionLLenado,  strLecturaInicialVenta);



                    if (AplicaServicioWindows)
                    {
                        if (VentaFinalizada != null)
                        {
                            VentaFinalizada(CaraID, strTotalVenta, strPrecio, strLecturaFinalVenta, strVolumen, bytProducto, IdManguera, PresionLLenado, strLecturaInicialVenta);
                        }
                    }



                    ////if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 12-03-11 
                    ////{//Se asegura obtener siempre la lectura Inicial de volumen e Importe, para corregir el error de lecturas, en caso que el grado autorizado no sea el que despacho
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta;

                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte;

                    ////}




                }
                else  //las lecturas son = 0 Venta en Cero 
                {

                    //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada       
                    if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 07-04-2011
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado autorizado: " + PropiedadesCara[CaraEncuestada].GradoVentaInicial +
                            " - Grado que vendio: " + PropiedadesCara[CaraEncuestada].GradoVenta + " |Proceso|ProcesoFindeVenta ");
                        SWRegistro.Flush();

                        PropiedadesCara[CaraEncuestada].GradoVenta = PropiedadesCara[CaraEncuestada].GradoVentaInicial;


                        ProcesoFindeVenta(); // Se toman de nuevo las lecturas pro error en el Grado que reporto la venta.
                    }
                    else
                    {

                        if (AplicaServicioWindows)
                        {
                            if (VentaInterrumpidaEnCero != null)
                            {
                                VentaInterrumpidaEnCero(CaraID);
                            }
                        }

                        PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Venta en CERO");
                        SWRegistro.Flush();

                    }
                }



                //else
                //{
                //    SWRegistro.WriteLine(DateTime.Now + "|"  + CaraID +  "|Fallo|No acepto comando de obtencion de Precio en Final de Venta");
                //    SWRegistro.Flush();
                //}


                PropiedadesCara[CaraEncuestada].EsVentaParcial = false; //Borrara solo pruebas DCF
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
                        if (AplicaServicioWindows)
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

                        if (AplicaServicioWindows)
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

        public void Evento_FinalizarCambioTarjeta(byte Cara) { }
        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto) { }

        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string Guid, Decimal PresionLLenado)
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
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Excepcion|oEvento_VentaAutorizada: " + Excepcion);
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

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;
                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;
                            }

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
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;
                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;

                            }

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


        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)//Realizado por el remplazo del shared event, usa una interfaz en el proyecto Fabrica Protocolo
        { }

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
                    }
                }

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|oEvento_TurnoCerrado: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_FinalizarVentaPorMonitoreoCHIP(byte Cara)
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    PropiedadesCara[Cara].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(Cara) + "|Evento|Recibe Detencion por Monitoreo de Chip");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEVento_FinalizarVentaPorMonitoreoCHIP: " + Excepcion);
                SWRegistro.Flush();
            }

        }
        public void Evento_CerrarProtocolo()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Recibe evento de detencion de Protocolo");
                SWRegistro.Flush();
                CondicionCiclo = false;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|EventoCerrarProtocolo: " + Excepcion);
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

     
        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera DCF 09/07/2018
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
