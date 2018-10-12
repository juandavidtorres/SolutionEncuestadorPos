
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.Threading;         //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using POSstation.Protocolos;
using System.Net.Sockets;
using System.Net;



namespace POSstation.Protocolos
{
  public class PumpControl : iProtocolo
    {
        #region EventoDeProtocolo

        private bool AplicaWindows = false;
        private bool AplicaTramas = true;
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

        //VARIABLES DE CONTROL
        ComandoSurtidor ComandoCaras;   //Arreglo que almacena el COMANDO enviado al surtidor
        byte CaraEncuestada;            //CARA que se esta ENCUESTANDO
        bool ComandoAceptado;           //Determina si el comando fue aceptado o rechazado por el surtidor
        bool CondicionCiclo;            //Variable para garantizar el ciclo infinito
        bool FalloComunicacion;         //Establece si hubo error en la comunicación (Surtidor no contesta)        
        decimal PrecioEDS;				//Almacena el PRECIO vigente en la EDS, grabado en la Base de Datos
        decimal DensidadEDS;
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int BytesEsperados;             //Declara la cantidad de bytes esperados por Comando
        int ByteEsperadoRX;
        int eco;                        //Variable que toma un valor diferente de 0, cuado la interfase devuelve ECO
        string PuertoSurtidores;


        bool CondicionCiclo2 = true;
        bool EncuentaFinalizada = false;

        byte CaraTmp; // Utilizado para las caras con alias mas de 16 caras

        byte CaraID;//DCF Alias 


        //ENUMERACIONES UTILIZADA PARA CREAR VARIABLES        
        public enum ComandoSurtidor		//Define los posibles COMANDO que se envian al Surtidor
        {
            DetenerVenta,
            AutorizarVenta,
            TerminarVenta,
            Predeterminar,
            Estado,
            DatosDespacho,
            EstablecerPrecio,
            ObtenerPrecio,
            Volumen,
            Importe,
            Totales,
            VolumenPrevio,
            ImportePrevio,
            Error,
            EstablecerDensidad,
            Decimal,
            VersionFirmware,
            ObtenerPresion
        }
        public enum Errores				//Define los estados de ERROR entregados por el surtidor
        {
            ExcesodeFlujo,
            DisplayDesconectado,
            SensorMasico,
            OverflowImporte,
            OverflowVolumen,
            ValvulaNoCierra,
            Prede,
            Totales,
            InitiParametors,
            Comnicacion1
        }
        public enum Evento				//Define los diferentes EVENTOS del Surtidor
        {
            CambioTotales,
            CambioPrecioRemoto,
            CambioDensidadRemoto,
            ErrorCRCSerie,
            CambioPrecioLocal,
            CambioDensidadLocal,
            ComandoSerieInexistente
        }

        /*Tramas compuestas de bytes para comunicacion con SURTIDOR */
        byte[] TramaRx = new byte[5];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[5];   //Almacena la TRAMA A ENVIAR      



        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        public string Puerto;
        int Bytes_leidos;
        int BytesRecibidos = 0;

        TcpClient ClientePumpControl;
        NetworkStream Stream;

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        // SharedEventsFuelStation.CMensaje oEvento;                      //Controla la comunicacion entre las aplicaciones por medio de eventos

        //System.Timers.Timer PollingTimer = new System.Timers.Timer(20); //Definicion del TIMER DE ENCUESTA

        //Controla la comunicacion entre las aplicaciones por medio de eventos       


        //Diccionario donde se almacenan las Caras y sus propiedades
        Dictionary<byte, RedSurtidor> PropiedadesCara;

        //Instancia Arreglo de lecturas para reportar reactivación de cara
        System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

        AsyncCallback callBack = new AsyncCallback(CallBackMethod);

        //VARIABLES VARIAS
        string ArchivoRegistroSucesos; //Variable que almacen la ruta y el nombre del archivo que guarda inconsistencias en el proceso logico
        StreamWriter SWRegistro;

        string ArchivoTramas;
        StreamWriter SWTramas;              //Variable utilizada para escribir en el archivo


        int Error_Anterior = 0;

        #endregion

        #region METODOS PRINCIPALES

        //PUNTO DE ARRANQUE DE LA CLASE
        //public PumpControl(string Puerto, byte NumerodeCaras, byte CaraInicial, string strPrecioEDS, List<Cara> ListaPropiedadesCara)
        public PumpControl(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
                this.Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-PumpControl-Registro (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-PumpControl-Tramas (" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);


                //Escribe encabezado en archivo de Estados
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.02.19-1406"); 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.05.25-1753");// 2010.05.20. Walberto GasStations
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.06.15-1512");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.07.12-0848");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.27-0000");//Para Fullstations  //DCF para no tomar lecturas cuando no se autorice la Venta. 2010/09/29

                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2011.03.25-1000"); //Reset del elemento que indica que la Cara debe ser autorizada //DCF

                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2011.03.30-1700"); //predeterminacion por Volumen
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2011.07.21-1010"); //Dll Para chile No consultar la Presión de llenado
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2012.08.00-1115"); //if (PropiedadesCara[CaraEncuestada].AplicaControlPresionLLenado)//DCF 08/06/2012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2012.08.15-1135"); //"|Proceso|No AplicaControlPresionLLenado"
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2012.12.08-0827"); // retardo para bolivia para que el surtidor autorice llega la autorizacion muy rapido y no autorizas 08122012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2013.03.07-0450");  //  Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2013.05.20-1550");  //  Bandera para controla el envio del RequerirAutorizacion ---20/05/2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2013.11.28-1033");//Environment.CurrentDirectory  por  Application.StartupPath 
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2015.08.24-1529");//if (VentaParcial != null) //JD 24/08/2015
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2015.09.08-1225");//Log de error
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2015.09.30-1258");//DCF pruebas IMW detener la venta 30/09/2015
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2016.11.12-1948");//DCF 12-11-2016 PARA Loop  GST TCP-IP 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2018.03.08-1730");//DCF Archivos .txt 08/03/2018  
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2018.05.22- 1457");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2018.05.23- 1754"); //Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2018.05.24- 1823"); //Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
              
                SWRegistro.Flush();




                ////Instancia los eventos disparados por la aplicación cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //oEvento.CambioPrecio += new SharedEventsFuelStation.__CMensaje_CambioPrecioEventHandler(oEvento_CambioPrecio);
                //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                //oEvento.InactivarCaraCambioTarjeta += new SharedEventsFuelStation.__CMensaje_InactivarCaraCambioTarjetaEventHandler(oEvento_InactivarCaraCambioTarjeta);
                //oEvento.FinalizarCambioTarjeta += new SharedEventsFuelStation.__CMensaje_FinalizarCambioTarjetaEventHandler(oEvento_FinalizarCambioTarjeta);
                //oEvento.CambiarDensidad += new SharedEventsFuelStation.__CMensaje_CambiarDensidadEventHandler(oEvento_CambiarDensidad);
                //oEvento.FinalizarVentaPorMonitoreoCHIP += new SharedEventsFuelStation.__CMensaje_FinalizarVentaPorMonitoreoCHIPEventHandler(oEvento_FinalizarVentaPorMonitoreoCHIP);
                //oEvento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEvento_CerrarProtocolo);

                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 4800;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.None;
                    PuertoCom.ReadBufferSize = 1024;
                    PuertoCom.WriteBufferSize = 1024;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception ex)
                    {
                        throw ex; //throw new Exception ("Puerto de surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }
                //Armar diccionario
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                HiloCicloCaras.Start();

                //Almacena el Precio de Venta establecido para la EDS
                //PrecioEDS = Convert.ToDecimal(strPrecioEDS);

                PuertoSurtidores = Puerto;


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase PumpControl " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + PropiedadesCara.Count + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                throw Excepcion;
            }
        }

        //(EsTCPIP, DireccionIP, Puerto, PropiedadesCara, true, true);
        public PumpControl(bool TCPIP_ETH, string DireccionIP_ETH, string Puerto_ETH, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {


                EsTCPIP = TCPIP_ETH;
                Puerto = Puerto_ETH;
                DireccionIP = DireccionIP_ETH;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-PumpControl-Registro (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-PumpControl-Tramas (" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);



                if (EsTCPIP)
                {
                    try
                    {
                        //Crea y abre la conexión con el Servidor
                        ClientePumpControl = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClientePumpControl.GetStream();

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }


                }
                else if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 4800;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.None;
                    PuertoCom.ReadBufferSize = 1024;
                    PuertoCom.WriteBufferSize = 1024;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception ex)
                    {
                        throw ex; //throw new Exception ("Puerto de surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }

                //Escribe encabezado en archivo de Estados
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.02.19-1406"); 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.05.25-1753");// 2010.05.20. Walberto GasStations
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.06.15-1512");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.07.12-0848");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.27-0000");//Para Fullstations  //DCF para no tomar lecturas cuando no se autorice la Venta. 2010/09/29

                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2011.03.25-1000"); //Reset del elemento que indica que la Cara debe ser autorizada //DCF

                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2011.03.30-1700"); //predeterminacion por Volumen
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2011.07.21-1010"); //Dll Para chile No consultar la Presión de llenado
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2012.08.00-1115"); //if (PropiedadesCara[CaraEncuestada].AplicaControlPresionLLenado)//DCF 08/06/2012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2012.08.15-1135"); //"|Proceso|No AplicaControlPresionLLenado"
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2012.12.08-0827"); // retardo para bolivia para que el surtidor autorice llega la autorizacion muy rapido y no autorizas 08122012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2013.03.07-0450");  //  Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2013.05.20-1550");  //  Bandera para controla el envio del RequerirAutorizacion ---20/05/2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl modificado 2013.11.28-1033");//Environment.CurrentDirectory  por  Application.StartupPath 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2014.08.13-1503");//TCPIP
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2015.08.24-1529");//if (VentaParcial != null) //JD 24/08/2015
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2015.09.08-1225");//Log de error 
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2015.09.30-1258");//DCF pruebas IMW detener la venta 30/09/2015
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2016.11.12-1948");//DCF 12-11-2016 PARA Loop  GST TCP-IP 
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2018.05.22- 1457");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo PumpControl TCP_IP modificado 2018.05.24- 1823"); //Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                SWRegistro.Flush();


                //Armar diccionario
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                HiloCicloCaras.Start();

                //Almacena el Precio de Venta establecido para la EDS
                //PrecioEDS = Convert.ToDecimal(strPrecioEDS);

                PuertoSurtidores = Puerto;


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase PumpControl " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + PropiedadesCara.Count + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                throw Excepcion;
            }
        }



        //CICLO INFINITO DE RECORRIDO DE LAS CARAS (REEMPLAZO DEL TIMER)
        private void CicloCara()
        {
            try
            {

                CondicionCiclo = true; //DCF

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
                                EncuentaFinalizada = false;//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017

                                CaraEncuestada = ORedCaras.Cara;
                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                if (ProcesoEnvioComando(ComandoSurtidor.Estado))
                                    TomarAccion();

                                EncuentaFinalizada = true;//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                            }
                        }
                            
                        Thread.Sleep(0);
                    }
                }
            }
            catch (Exception Excepcion)
            {

                EncuentaFinalizada = true;
                CondicionCiclo2 = true;

                string MensajeExcepcion = "Excepcion en el Metodo CicloCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //REALIZA PROCESO COMPLETO DE ENVÍO DE TRAMA Y COMPROBACIÓN DE ERRORES EN LA TRAMA DE RESPUESTA
        private bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

                //Arma la trama de Transmision
                ArmarTramaTx(ComandoaEnviar);

                //Reintentos de envio de comando 
                do
                {

                    if (EsTCPIP)
                        EnviarComando_TCPIP();

                    else
                        EnviarComando();

                    //Analiza la información recibida si se espera respuesta del Surtidor
                    if (BytesEsperados > 0)
                    {
                        if (EsTCPIP)
                            RecibirInformacion_TCPIP();
                        else
                            RecibirInformacion();

                        Reintentos += 1;
                    }
                } while (FalloComunicacion == true && Reintentos < 3);

                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion == true)
                {
                    //Si la cara se va a Inactivar
                    if (PropiedadesCara[CaraEncuestada].InactivarCara == true)
                    {
                        PropiedadesCara[CaraEncuestada].InactivarCara = false;
                        PropiedadesCara[CaraEncuestada].Activa = false;
                        string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                        IniciarCambioTarjeta(CaraEncuestada, Puerto);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivacion en Fallo de Comunicacion");
                        SWRegistro.Flush();
                    }
                    //Si la cara se va a activar
                    if (PropiedadesCara[CaraEncuestada].ActivarCara == true)
                    {
                        PropiedadesCara[CaraEncuestada].Activa = false;
                        string Mensaje = "No se puede ejecutar activacion: Cara " + CaraEncuestada + " con fallo de comunicacion";
                        bool Imprime = true;
                        bool Terminal = false;
                        string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                        ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo ejecutar activacion. Fallo de comunicacion");
                        SWRegistro.Flush();
                    }

                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {
                        string MensajeErrorLectura = "Error en Comunicacion con Surtidor";
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                            bool EstadoTurno = false;
                            PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                            CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                    }

                    //Ingresa a este condicional si el surtidor NO responde y si no se ha logueado aún la falla
                    if (!PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Perdida de comunicacion. Estado: " +
                            PropiedadesCara[CaraEncuestada].Estado + " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].FalloReportado = true;
                    }
                }
                else
                {
                    if (PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Reestablecimiento de comunicación. Estado: " + PropiedadesCara[CaraEncuestada].Estado +
                            " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].FalloReportado = false;
                    }
                }
                return !FalloComunicacion;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ProcesoEnvioComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        private void ArmarTramaTx(ComandoSurtidor ComandoTx)
        {
            try
            {
                //Asigna a la cara a encuestar el comando que fue enviado
                ComandoCaras = ComandoTx;

                //Obtiene el numero del surtidor
                byte Surtidor = Convert.ToByte(ObtenerSurtidor(CaraEncuestada));

                //Dependiendo del Comando a Enviar se Construye la Trama
                switch (ComandoTx)
                {
                    case ComandoSurtidor.DetenerVenta:
                        TimeOut = 100;
                        TramaTx = new byte[6] { 0xAA, Surtidor, 0x00, 0x01, 0x00, 0x00 };
                        BytesEsperados = 0;
                        break;
                    case ComandoSurtidor.AutorizarVenta:
                        TimeOut = 500;//100 Normal 
                        TramaTx = new byte[6] { 0xAA, Surtidor, 0x00, 0x01, 0x01, 0x00 };
                        // BytesEsperados = 0;
                        BytesEsperados = 5;//DCF 12-11-2016 PARA Loop  GST TCP-IP
                        break;
                    case ComandoSurtidor.TerminarVenta:
                        TimeOut = 100;
                        TramaTx = new byte[6] { 0xAA, Surtidor, 0x00, 0x01, 0x02, 0x00 };
                        //BytesEsperados = 0;
                        BytesEsperados = 5;//DCF 12-11-2016 PARA Loop  GST TCP-IP - DCF 12-11-2016 PARA Loop  GST TCP-IP 
                        break;
                    case ComandoSurtidor.Predeterminar:
                        TimeOut = 150;
                        TramaTx = new byte[10] { 0xAA, Surtidor, 0x00, 0x01, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00 };

                        int ValorPredeterminado;
                        ValorPredeterminado = Convert.ToInt32(PropiedadesCara[CaraEncuestada].ValorPredeterminado *
                            PropiedadesCara[CaraEncuestada].FactorImporte);



                        //******************************************************************
                        // Predeterminacion por volumen Solicitud para Chile DCF 30-03-2011
                        //******************************************************************
                        //PropiedadesCara[Cara].PredeterminarVolumen = true;
                        //PropiedadesCara[Cara].PredeterminarImporte = false;
                        if (PropiedadesCara[CaraEncuestada].PredeterminarVolumen)
                        {
                            ValorPredeterminado = Convert.ToInt32(PropiedadesCara[CaraEncuestada].ValorPredeterminado *
                            PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1 * PropiedadesCara[CaraEncuestada].FactorImporte);

                            PropiedadesCara[CaraEncuestada].PredeterminarVolumen = false;

                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|ValorPredeterminado = " + ValorPredeterminado
                            //    + " | Valor Programado = " + PropiedadesCara[CaraEncuestada].ValorPredeterminado +
                            //    " | Precio Venta = " + PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1 +
                            //    " | Factorprecio = " + PropiedadesCara[CaraEncuestada].FactorImporte
                            //    );
                            //SWRegistro.Flush();
                        }

                        //******************************************************************
                        //******************************************************************



                        string strDatosPredeterminado = Convert.ToString(ValorPredeterminado, 16).PadLeft(8, '0');
                        int j = 2;
                        for (int i = 5; i < 8; i++)
                        {
                            TramaTx[i] = Convert.ToByte(strDatosPredeterminado.Substring(strDatosPredeterminado.Length - j, 2), 16);
                            j += 2;
                        }
                        BytesEsperados = 5;
                        break;
                    case ComandoSurtidor.Estado:
                        TimeOut = 250;//200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x82, 0x00 };
                        BytesEsperados = 5;
                        break;
                    case ComandoSurtidor.DatosDespacho:
                        TimeOut = 300; //Antes 200
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x83, 0x00 };
                        BytesEsperados = 12;
                        break;
                    case ComandoSurtidor.ObtenerPrecio:
                        TimeOut = 300;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x84, 0x00 };
                        BytesEsperados = 6;
                        break;
                    case ComandoSurtidor.EstablecerPrecio:
                        TimeOut = 200;
                        int PrecioEstablecido;
                        TramaTx = new byte[7] { 0xAA, Surtidor, 0x00, 0x04, 0x00, 0x00, 0x00 };
                        PrecioEstablecido = Convert.ToInt32(PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1 * PropiedadesCara[CaraEncuestada].FactorPrecio);
                        string strDatos = Convert.ToString(PrecioEstablecido, 16).PadLeft(4, '0');
                        int k = 2;
                        for (int i = 4; i < (4 + strDatos.Length / 2); i++)
                        {
                            TramaTx[i] = Convert.ToByte(strDatos.Substring(strDatos.Length - k, 2), 16);
                            k += 2;
                        }
                        BytesEsperados = 5;
                        break;
                    case ComandoSurtidor.Volumen:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x85, 0x00 };
                        BytesEsperados = 8;
                        break;
                    case ComandoSurtidor.Importe:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x86, 0x00 };
                        BytesEsperados = 8;
                        break;
                    case ComandoSurtidor.Totales:
                        TimeOut = 400;//Antes 300
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x87, 0x00 };
                        BytesEsperados = 12;
                        break;
                    case ComandoSurtidor.VolumenPrevio:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x88, 0x00 };
                        BytesEsperados = 8;
                        break;
                    case ComandoSurtidor.ImportePrevio:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x89, 0x00 };
                        BytesEsperados = 8;
                        break;
                    case ComandoSurtidor.Error:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x8A, 0x00 };
                        BytesEsperados = 6;
                        break;

                    //case ComandoSurtidor.Densidad:
                    //    TimeOut = 200;
                    //    TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x8B, 0x00 };
                    //    BytesEsperados = 0;
                    //    break;

                    case ComandoSurtidor.EstablecerDensidad:
                        TimeOut = 200;
                        int Densidad = Convert.ToInt16(this.DensidadEDS * 1000);
                        TramaTx = new byte[7] { 0xAA, Surtidor, 0x00, 0x0B, 0x00, 0x00, 0x00 };
                        strDatos = Convert.ToString(Densidad, 16).PadLeft(4, '0');
                        /*
                        Trama[4]= Convert.ToByte(strDatos.Substring(2, 2), 16);
                        Trama[5] = Convert.ToByte(strDatos.Substring(0, 2), 16);
                        */
                        for (int i = 8; i <= 8 + strDatos.Length / 2; i += 2)
                            TramaTx[i / 2] = Convert.ToByte(strDatos.Substring(10 - i, 2), 16);
                        BytesEsperados = 5;
                        break;

                    case ComandoSurtidor.Decimal:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x8C, 0x00 };
                        BytesEsperados = 5;
                        break;
                    case ComandoSurtidor.VersionFirmware:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x8D, 0x00 };
                        BytesEsperados = 6;
                        break;
                    case ComandoSurtidor.ObtenerPresion:
                        TimeOut = 200;
                        TramaTx = new byte[5] { 0xAA, Surtidor, 0x00, 0x93, 0x00 };
                        BytesEsperados = 6;
                        break;
                }

                //Calcula la longitud del mensaje y el Caracter de Redundancia Ciclica (CRC)
                TramaTx[2] = Convert.ToByte(TramaTx.Length - 3);
                if (CaraEncuestada % 2 == 0) TramaTx[3] = Convert.ToByte(Convert.ToInt16(TramaTx[3]) | 0x40);
                TramaTx[TramaTx.Length - 1] = ObtenerCRC(TramaTx);

                //Variable momentanea, mientras se define como se vuelve funcional esta situacion
                eco = Convert.ToByte(TramaTx.Length);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ArmarTramaTx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
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


                //DCF
                /////////////////////////////////////////////////////////////////////////////////
                //LOGUEO DE TRAMA TRANSMITIDA
                string strTramaTx = "";
                for (int i = 0; i <= TramaTx.Length - 1; i++)
                    strTramaTx += TramaTx[i].ToString("X2") + " | ";

                SWTramas.WriteLine(DateTime.Now + "|Tx|Cara " + CaraEncuestada + ": |" + strTramaTx);
                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////


                //Tiempo muerto mientras el Surtidor Responde
                Thread.Sleep(TimeOut);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo EnviarComando " + ComandoCaras + ": " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
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
                    VerificarConexion();
                    Stream.Write(TramaTx, 0, TramaTx.Length);
                    Stream.Flush();
                }
                catch (System.Net.Sockets.SocketException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        Thread.Sleep(200);
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                        Stream.ReadTimeout = 6000;

                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();

                    }
                }
                catch (System.IO.IOException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        Thread.Sleep(200);
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                        Stream.ReadTimeout = 6000;

                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();

                    }

                }
                catch (System.Exception)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        Thread.Sleep(200);
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                        Stream.ReadTimeout = 6000;

                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
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
                    "|" + CaraEncuestada + "|Tx|" + strTrama);

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
                //Thread.Sleep(150);
                //VerificarConexion();
                //Stream.Write(TramaTx, 0, TramaTx.Length);
                //Stream.Flush();
            }
        }

        public void RecibirInformacion_TCPIP()
        {
            try
            {
                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                // BytesEsperados = BytesEsperados + eco; //DCF 12-11-2016 PARA Loop  GST TCP-IP
                ByteEsperadoRX = 0; //DCF 12-11-2016 PARA Loop  GST TCP-IP

                ByteEsperadoRX = BytesEsperados + eco;

                byte[] TramaRxTemporal = new byte[255];// new byte[BytesEsperados]; /// leer todo lo que se pueda para TCP  //DCF 12-11-2016 PARA Loop  GST TCP-IP

                if (!Stream.DataAvailable)
                    Thread.Sleep(40);

                if (Stream.DataAvailable)
                {

                    if (Stream.CanRead)
                    {
                        do
                        {
                            Thread.Sleep(50);//DCF 12-11-2016 PARA Loop  GST TCP-IP
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                        } while (Stream.DataAvailable);
                    }



                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    ////if (Bytes_leidos == BytesEsperados)
                    //{
                    LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                    FalloComunicacion = false;


                    //Definicion de Trama Temporal
                    byte[] TramaTemporal = new byte[Bytes_leidos];

                    ////Almacena informacion en la Trama Temporal para luego eliminarle el eco
                    // PuertoCom.Read(TramaTemporal, 0, Bytes_leidos);
                    //PuertoCom.DiscardInBuffer();

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
                        "|" + CaraEncuestada + "|Rx|" + strTrama);

                    SWTramas.Flush();
                    /////////////////////////////////////////////////////////////////////////////////
                    // if (Bytes_leidos == BytesEsperados) //DCF 12-11-2016 PARA Loop  GST TCP-IP
                    if (Bytes_leidos == ByteEsperadoRX) //DCF 12-11-2016 PARA Loop  GST TCP-IP
                    {
                        AnalizarTrama();

                    }
                    else if (FalloComunicacion == false)
                    {
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
                string MensajeExcepcion = "Excepcion en el Metodo RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        private void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;

                //eco = 0;// Solo Prueba DCF
                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                BytesEsperados = Convert.ToByte(BytesEsperados + eco);

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                if (Bytes == BytesEsperados)
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


                    //DCF
                    ///////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA RECIBIDA
                    string strTramaRx = "";
                    for (int i = 0; i <= TramaRx.Length - 1; i++)
                        strTramaRx += TramaRx[i].ToString("X2") + " | ";

                    SWTramas.WriteLine(DateTime.Now + "|Rx|Cara " + CaraEncuestada + ": |" + strTramaRx);
                    SWTramas.Flush();
                    /////////////////////////////////////////////////////////////////////////////////





                    AnalizarTrama();
                }
                else if (FalloComunicacion == false)
                    FalloComunicacion = true;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecibirInformacion " + ComandoCaras + ": " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        //public void VerifySizeFile() //Logueo
        //{
        //    try
        //    {
        //        FileInfo FileInf = new FileInfo(ArchivoTramas);

        //        if (FileInf.Length > 50000000)
        //        {
        //            SWTramas.Close();
        //            ArchivoTramas = Environment.CurrentDirectory + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-TramasPumpControl.(" + PuertoCom.PortName + ").txt";
        //            SWTramas = File.AppendText(ArchivoTramas);
        //        }

        //        FileInf = new FileInfo(ArchivoRegistroSucesos);
        //        if (FileInf.Length > 30000000)
        //        {
        //            SWRegistro.Close();
        //            //Crea archivo para almacenar inconsistencias en el proceso logico
        //            ArchivoRegistroSucesos = Environment.CurrentDirectory + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-SucesosPumpControl.(" + PuertoCom.PortName + ").txt";
        //            SWRegistro = File.AppendText(ArchivoRegistroSucesos);
        //        }
        //    }

        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepción Verificación del tamaño de Logueo: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + "|" + "|Excepcion|" + MensajeExcepcion);
        //        SWRegistro.Flush();
        //    }

        //}


        //LEE Y ALMACENA LA TRAMA RECIBIDA


        public void VerifySizeFile()
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-PumpControl-Tramas (" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                FileInf = new FileInfo(ArchivoRegistroSucesos);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-PumpControl-Registro (" + Puerto + ").txt";
                    SWRegistro = File.AppendText(ArchivoRegistroSucesos);
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|VerifySizeFile: " + Excepcion);
                SWRegistro.Flush();
            }

        }

        public void VerificarConexion()
        {
            int iReintento = 0;
            string Comando = "";
            try
            {
                if (ClientePumpControl == null)
                {
                    Boolean EsInicializado = false;
                    while (!EsInicializado)
                    {
                        try
                        {
                            ClientePumpControl = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));

                            if (ClientePumpControl == null)
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

                        if (ClientePumpControl != null)
                        {
                            //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Inicializada - Ip: " + DireccionIP + " Puerto: " + Puerto);
                            //SWRegistro.Flush();
                            EsInicializado = true;
                        }
                    }
                }

                Boolean estadoAnterior = true;
                if (!this.ClientePumpControl.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClientePumpControl.Client.BeginDisconnect(true, callBack, ClientePumpControl);

                    }

                    catch (Exception e)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginDisconnect: " + e.Message);
                        SWRegistro.Flush();
                        Thread.Sleep(200);
                    }
                }
                else
                {
                    //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Conexion Abierta");
                    //SWRegistro.Flush();
                    estadoAnterior = true;
                }



                while (!this.ClientePumpControl.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClientePumpControl.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClientePumpControl);
                        //ClientePumpControl.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClientePumpControl.Client.Connected)
                        {
                            Thread.Sleep(200);
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

                this.Stream = ClientePumpControl.GetStream();

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

        public void LimpiarSockets()
        {
            try
            {
                //ClientePumpControl.Client.Disconnect(false);  
                ClientePumpControl.Client.Close();
                ClientePumpControl.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClientePumpControl = null;
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|LimpiarSockets:" + ex.Message);
                SWRegistro.Flush();

            }

        }

        void LimpiarVariableSocket()
        {
            try
            {
                ClientePumpControl.Close();
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
                ClientePumpControl = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClientePumpControl.GetStream();
                if (this.ClientePumpControl.Client.Connected == true)
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



        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void AnalizarTrama()
        {
            try
            {
                //Si no hay integridad en la trama regresa arrojando error
                if (!ComprobacionTramaRx())
                    FalloComunicacion = true;
                else
                {
                    //Dependiendo del comando enviado, analiza la trama respuesta
                    switch (ComandoCaras)
                    {
                        case ComandoSurtidor.Estado:
                            AsignarEstado();
                            break;
                        case ComandoSurtidor.DatosDespacho:
                            //Asignacion Total Venta y Volumen Venta. Aplica para Parciales de Despacho y Totales de Venta
                            PropiedadesCara[CaraEncuestada].Volumen = ObtenerValor(TramaRx, 3, 4) / PropiedadesCara[CaraEncuestada].FactorVolumen;
                            PropiedadesCara[CaraEncuestada].TotalVenta = ObtenerValor(TramaRx, 7, 4) / PropiedadesCara[CaraEncuestada].FactorImporte;
                            break;
                        case ComandoSurtidor.ObtenerPrecio:
                            //Almacena el precio de venta de la cara
                            PropiedadesCara[CaraEncuestada].PrecioCara = ObtenerValor(TramaRx, 3, 2) / PropiedadesCara[CaraEncuestada].FactorPrecio;
                            break;
                        case ComandoSurtidor.EstablecerPrecio:
                        case ComandoSurtidor.Predeterminar:
                        case ComandoSurtidor.AutorizarVenta: //DCF 12-11-2016 PARA Loop  GST TCP-IP 
                        case ComandoSurtidor.TerminarVenta://DCF 12-11-2016 PARA Loop  GST TCP-IP 
                            //Indica si el comando cambio de precio fue establecido exitosamente o no
                            if (TramaRx[3] == 0x60)
                                ComandoAceptado = true;
                            else
                                ComandoAceptado = false;
                            break;
                        case ComandoSurtidor.Totales:
                            //Almacena el valor de la lectura tomada
                            PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura = ObtenerValor(TramaRx, 3, 4) / PropiedadesCara[CaraEncuestada].FactorTotalizador;
                            break;
                        case ComandoSurtidor.Error:
                            // PropiedadesCara[CaraEncuestada].ErrorCara = TramaRx[3];
                            PropiedadesCara[CaraEncuestada].CodigoError = TramaRx[3];
                            break;
                        case ComandoSurtidor.EstablecerDensidad:
                            if (TramaRx[3] == 0x60)
                            {
                                //Cambio de Densidad exitoso
                                PropiedadesCara[CaraEncuestada].CambiarDensidad = false;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Cambio de Densidad Exitoso: " + DensidadEDS);
                                SWRegistro.Flush();
                            }
                            else
                            {
                                //Lanzar Evento de Registro de cambio de Densidad
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Cambio de Densidad Fallido");
                                SWRegistro.Flush();
                            }
                            break;
                        case ComandoSurtidor.Decimal:
                            /* Define el factor de división del Valor Precio e Importe devuelto por el Surtidor
                             * con base en el Código de Punto Decimal según el Manual Técnico de Pump Control*/
                            if (PropiedadesCara[CaraEncuestada].FactorImporte == 0 ||
                                PropiedadesCara[CaraEncuestada].FactorPrecio == 0)
                            {
                                switch (TramaRx[3])
                                {
                                    case 0:
                                        PropiedadesCara[CaraEncuestada].FactorPrecio = 1;
                                        PropiedadesCara[CaraEncuestada].FactorImporte = 1;
                                        break;
                                    case 1:
                                        PropiedadesCara[CaraEncuestada].FactorPrecio = 10;
                                        PropiedadesCara[CaraEncuestada].FactorImporte = 1;
                                        break;
                                    case 2:
                                        PropiedadesCara[CaraEncuestada].FactorPrecio = 100;
                                        PropiedadesCara[CaraEncuestada].FactorImporte = 10;
                                        break;
                                    case 3:
                                        PropiedadesCara[CaraEncuestada].FactorPrecio = 1000;
                                        PropiedadesCara[CaraEncuestada].FactorImporte = 100;
                                        break;
                                    case 4:
                                        PropiedadesCara[CaraEncuestada].FactorPrecio = 100;
                                        PropiedadesCara[CaraEncuestada].FactorImporte = 100;
                                        break;
                                }
                            }

                            if (PropiedadesCara[CaraEncuestada].FactorTotalizador == 0)
                                PropiedadesCara[CaraEncuestada].FactorTotalizador = 100;
                            if (PropiedadesCara[CaraEncuestada].FactorVolumen == 0)
                                PropiedadesCara[CaraEncuestada].FactorVolumen = 100;
                            break;
                        case ComandoSurtidor.VersionFirmware:

                            PropiedadesCara[CaraEncuestada].VersionFirmware = ObtenerValor(TramaRx, 3, 2) / 100;
                            break;

                        case ComandoSurtidor.ObtenerPresion:
                            PropiedadesCara[CaraEncuestada].ListaGrados[0].PresionLlenado = Convert.ToInt16(ObtenerValor(TramaRx, 3, 2));
                            break;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo AnalizarTrama: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void AsignarEstado()
        {
            try
            {
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                {
                    PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;
                    Error_Anterior = 0;  //DCF 15-09-2015 pruebas IMW
                }
                switch (TramaRx[3])
                {
                    case (0x02):
                        /* CONTINGENCIA: Se fuerza logicamente el Estado Fin de Venta, 
                         * en caso de que se corte el suministro electrico mientras una venta estaba en proceso.
                         * El surtidor, para el programa, pasa de estado "En Despacho" a estado "En Espera" */

                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                        else
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlEspera;
                        break;
                    case (0x03):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlDespacho;
                        break;
                    case (0x04):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlFinDespacho;
                        break;
                    case (0x05):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlProgramacion;
                        break;
                    case (0x06):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlError;
                        break;
                    case (0x07):
                        /* CONTINGENCIA: Se fuerza logicamente el Estado Fin de Venta, 
                         * en caso de que se corte el suministro electrico mientras una venta estaba en proceso.
                         * El surtidor, para el programa, pasa de estado "En Despacho" a estado "Por Autorizar" */
                        /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Trata de finalizar venta en estado Transicion");
                            SWRegistro.Flush();
                        }
                        /*--*/
                        else
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlPorAutorizar1;
                        break;
                    case (0x08):
                        /* CONTINGENCIA: Se fuerza logicamente el Estado Fin de Venta, 
                         * en caso de que se corte el suministro electrico mientras una venta estaba en proceso.
                         * El surtidor, para el programa, pasa de estado "En Despacho" a estado "Por Autorizar" */
                        /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Trata de finalizar venta en estado Init_Despacho");
                            SWRegistro.Flush();
                            //EstadoCaras[CaraEncuestada - 1] = EstadoCara.FinDespacho;                       
                        }
                        /*--*/
                        else
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlPorAutorizar2;
                        break;
                    case (0x09):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlIniciaDespacho;
                        break;
                    case (0x0A):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlPredeterminado;
                        break;
                    case (0x0B):
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.PumpControlEsperandoFinFlujo;
                        break;
                    default:
                        /*EstadoAnterior[CaraEncuestada - 1] = EstadoCaras[CaraEncuestada - 1];
                         * EstadoCaras[CaraEncuestada - 1] = EstadoCara.Desconocido; */

                        break;
                }

                //Almacena en archivo el estado actual del surtidor
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|" + PropiedadesCara[CaraEncuestada].Estado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo AsignarEstado " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        private void TomarAccion()
        {
            try
            {
                int Reintentos = 0;
                //Solamente ingresa a esta parte de código cuando no se ha inicializado la cara (inicio de programa)
                if (PropiedadesCara[CaraEncuestada].CaraInicializada == false)
                {
                    //Si la cara esta en reposo o pidiendo autorización se inicializa la posición
                    //Comentado para obligar a que las mangueras están colgadas cuando se reinicie
                    //if ((EstadoCaras[CaraEncuestada - 1] == EstadoCara.Espera) || (EstadoCaras[CaraEncuestada - 1] == EstadoCara.PorAutorizar1) || (EstadoCaras[CaraEncuestada - 1] == EstadoCara.PorAutorizar2))
                    if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.PumpControlEspera)
                    {
                        //Obtiene el factor de division si no se han configurado por el CONFIGURADOR
                        if (ProcesoEnvioComando(ComandoSurtidor.Decimal))
                        {
                            //Pide la Versión del Firmware
                            if (ProcesoEnvioComando(ComandoSurtidor.VersionFirmware))
                            {
                                //Logueo de Firmware del Surtidor
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Version del Firmware " + PropiedadesCara[CaraEncuestada].VersionFirmware);
                                SWRegistro.Flush();
                            }
                            else
                            {
                                //Error en obtención del Firmware
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando VersionFirmware");
                                SWRegistro.Flush();
                            }

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Toma lecturas inicio de programa");
                            SWRegistro.Flush();

                            //Realiza toma de lecturas
                            if (TomarLecturas())
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura Inicial inicio de programa: " +
                                    PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura);
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura;
                                //Cambia bandera inidicando que la cara fue inicializada correctamente
                                PropiedadesCara[CaraEncuestada].CaraInicializada = true;
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No obtuvo Lectura en inicio de programa");
                                SWRegistro.Flush();
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Decimal");
                            SWRegistro.Flush();
                        }
                    }
                }

                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (PropiedadesCara[CaraEncuestada].Estado)
                {
                    case (EstadoCara.PumpControlEspera):

                        ////  Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                        PropiedadesCara[CaraEncuestada].PeticionAutorizacion = false; // se desactiva para que se pueda enviar el requerimiento de Autorizacion.

                        PropiedadesCara[CaraEncuestada].DetenerVentaCara = false;

                        //Reset del elemento que indica que la Cara debe ser autorizada //DCF
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara)
                        {
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara Desautorizada en PumpControlEspera");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            IniciarCambioTarjeta(CaraEncuestada, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivacion en Estado Espera");
                            SWRegistro.Flush();

                            //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante inactivación
                            if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                            {
                                string MensajeErrorLectura = "Cara Inactivada";
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = false;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = true;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                            }

                            //Sale del Caso si se inactiva
                            break;
                        }

                        //Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara == true)
                        {
                            if (TomarLecturaActivacionCara())
                            {
                                //Instancia Array para reportar las lecturas
                                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                                ArrayLecturas.CopyTo(LecturasEnvio);
                                //Lanza Evento para reportar las lecturas después de un cambio de tarjeta
                                LecturasCambioTarjeta(LecturasEnvio);
                                //Inicializa bandera que indica la activación de una cara
                                PropiedadesCara[CaraEncuestada].ActivarCara = false;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Activacion en Estado Espera. Lectura: " + LecturasEnvio);
                                SWRegistro.Flush();
                            }
                        }

                        /*- Fecha de Inclusión: 2008/03/06 17:40 -*/
                        //Si el Teclado del surtidor estaba siendo manipulado (Estado Anterior: TECLADO)
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.PumpControlProgramacion)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia toma de lecturas despues de Estado Programacion");
                            SWRegistro.Flush();
                            //Toma la lectura para tenerla en cuenta en la siguiente venta
                            if (TomarLecturas())
                            {
                                if (PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta != PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura inicial antes de Programacion: " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta + " - Lectura despues de programacion: " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura);
                                    SWRegistro.Flush();
                                    PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura;
                                }
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo tomar lectura despues de Estado Teclado");
                                SWRegistro.Flush();
                            }
                        }
                        /*--*/

                        //Informa cambio de estado 
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                        {
                            int mangueraColgada = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                            CaraEnReposo(CaraEncuestada, mangueraColgada);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara en Espera");
                            SWRegistro.Flush();


                            ////DCF 15-09-2015 pruebas IMW
                            //if (!ProcesoEnvioComando(ComandoSurtidor.DetenerVenta))
                            //{
                            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando DetenerVenta");
                            //    SWRegistro.Flush();
                            //}
                            //else
                            //{
                            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento| comando DetenerVenta OK");
                            //      SWRegistro.Flush();
                            //}


                        }
                        ///*- 29/12/2009 Se comenta por presunto causante de pérdida de ventas ---*/
                        ////Reset del elemento que indica que la Cara debe ser autorizada
                        //if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                        //{
                        //    PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                        //    /*- 19/09/2009 --------------------*/
                        //    VentaInterrumpidaEnCero( CaraEncuestada);
                        //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Venta en CERO en Cara en Espera");
                        //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Elimina registro de Venta Parcial");
                        //    SWRegistro.Flush();
                        //    /*- 19/09/2009 --------------------*/
                        //}
                        /*-------------------------------------------------------------------------*/

                        //Revisa si las lecturas deben ser tomadas (Evento Apertura o Cierre de Turno)
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            LecturaAperturaCierre();

                        //AÑADIDO  POR DCF
                        if (PropiedadesCara[CaraEncuestada].CambiarDensidad == true)
                        {
                            //DCF
                            do
                            {
                                if (!ProcesoEnvioComando(ComandoSurtidor.EstablecerDensidad))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Densidad");
                                    SWRegistro.Flush();
                                }
                                Reintentos += 1;
                                Thread.Sleep(20);
                            }
                            while (FalloComunicacion == true && Reintentos < 3);
                            PropiedadesCara[CaraEncuestada].CambiarDensidad = false;
                            //DCF no enviear mas de 3 veces el comando Establecer Densidad si el surtidor no responde a este comando 
                        }


                        break;

                    case (EstadoCara.PumpControlIniciaDespacho):
                    case (EstadoCara.PumpControlDespacho):
                        //Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara == true)
                        {
                            string Mensaje = "No se puede ejecutar inactivacion: Cara " + CaraEncuestada + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar inactivacion. Cara en " + PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraEncuestada + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puedo ejecutar activacion. Cara en " + PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho                    
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }


                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (!PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        {
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                            //DCF pruebas IMW detener la venta 30/09/2015
                            if (!PropiedadesCara[CaraEncuestada].AutorizarCara)
                            {
                                PropiedadesCara[CaraEncuestada].DetenerVentaCara = true;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|***** Se envia Detención de Despacho Venta sin autorizacion de sistema *****");
                                SWRegistro.Flush();
                            }
                        }


                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara)
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;


                        /*- Fecha de Inclusión: 2009/05/20-*/
                        //Envia comando al surtidor para detener la venta en curso

                        if (PropiedadesCara[CaraEncuestada].DetenerVentaCara)
                        {
                            PropiedadesCara[CaraEncuestada].DetenerVentaCara = false;
                            ProcesoEnvioComando(ComandoSurtidor.DetenerVenta);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Enviado Comando de Detener");
                            SWRegistro.Flush();
                        }

                        //Pedir Parciales de Venta.
                        if (ProcesoEnvioComando(ComandoSurtidor.DatosDespacho))
                        {
                            //Dispara evento al programa principal
                            string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N");
                            string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N");
                            if (VentaParcial != null) //JD 24/08/2015
                            {
                                VentaParcial(CaraEncuestada, strTotalVenta, strVolumen);
                            }

                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando DatosDespacho en parcial de venta");
                            SWRegistro.Flush();
                        }
                        break;

                    case EstadoCara.PumpControlFinDespacho:
                    case EstadoCara.FinDespachoForzado:

                        PropiedadesCara[CaraEncuestada].DetenerVentaCara = false;

                        //Reset del elemento que indica que la Cara debe ser autorizada //DCF
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara)
                        {
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara Desautorizada en PumpControlFinDespacho");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara == true)
                        {
                            string Mensaje = "No se puede ejecutar inactivacion: Cara " + CaraEncuestada + " en Fin de Venta";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo ejecutar inactivacion. Cara en " +
                                PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraEncuestada + " en Fin de Despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo ejecutar activacion. Cara en " +
                                PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho                    
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en Fin de Despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        /*- Fecha de Inclusión: 2009/09/07 -*/
                        //Si la venta no ha sido finalizada, se ejecuta proceso para finalizarla
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                            ProcesoFindeVenta();
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inconsistencia|Estado Fin de Despacho sin estar autorizada. Terminando venta");
                            SWRegistro.Flush();
                            ProcesoEnvioComando(ComandoSurtidor.TerminarVenta);
                        }
                        /*--*/

                        break;
                    case (EstadoCara.PumpControlProgramacion):
                        //Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;
                            //CaraActiva[CaraEncuestada - 1] = false;
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            IniciarCambioTarjeta(CaraEncuestada, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivacion en estado de Programacion");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraEncuestada + " en estado de Programacion";
                            bool Imprime = true;
                            bool Terminal = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo ejecutar activacion. Cara en " + PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                            break;
                        }

                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            // oEvento.InformarManipulacionTeclado(ref CaraEncuestada);

                            //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante Programación
                            if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                            {
                                string MensajeErrorLectura = "Cara visualizando lecturas";
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = false;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = true;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                            }
                        break;

                    case (EstadoCara.PumpControlError):
                        //Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;
                            //CaraActiva[CaraEncuestada - 1] = false;
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            IniciarCambioTarjeta(CaraEncuestada, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivacion en estado de Error");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraEncuestada + " en estado de Error";
                            bool Imprime = true;
                            bool Terminal = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo ejecutar activacion. Cara en " +
                                PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                            break;
                        }

                        /*- 19/09/2009 --------------------*/
                        //Si la venta fue autorizada por el Surtidor y no ha iniciado despacho se cancela el proceso
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara && !PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        {
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                            VentaInterrumpidaEnCero(CaraEncuestada);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Venta en CERO");
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Elimina registro de Venta Parcial");
                            SWRegistro.Flush();
                        }
                        /*- 19/09/2009 --------------------*/

                        //Envia comando para determinar el codigo del error
                        if (ProcesoEnvioComando(ComandoSurtidor.Error)) //Log de error 
                        {
                            //if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            //{
                            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|Estado de Error: " +
                            //        PropiedadesCara[CaraEncuestada].ErrorCara);
                            //    SWRegistro.Flush();
                            //}

                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado || Error_Anterior != PropiedadesCara[CaraEncuestada].CodigoError)

                            // if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|Estado de Error: " +
                                    PropiedadesCara[CaraEncuestada].CodigoError);
                                SWRegistro.Flush();


                                Error_Anterior = PropiedadesCara[CaraEncuestada].CodigoError;

                                switch (PropiedadesCara[CaraEncuestada].CodigoError)
                                {

                                    case (2):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Stagnant error: Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (3):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|LPI error: Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;


                                    case (4):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|High pressure error : Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;


                                    case (101):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Flow excess error : Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (102):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|LCD error: Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (103):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Sensor error : Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (104):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|999999 error : Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (105):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|9999 error : Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (106):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|U_sole error: Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (107):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|PT error: Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;

                                    case (202):
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fraud error: Codigo = " + PropiedadesCara[CaraEncuestada].CodigoError.ToString());
                                        SWRegistro.Flush();
                                        break;




                                }




                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Error");
                            SWRegistro.Flush();
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en estado de ERROR";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }
                        break;

                    case (EstadoCara.PumpControlPorAutorizar1):
                        //Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara == true)
                        {
                            string Mensaje = "No se puede ejecutar inactivacion: Cara " + CaraEncuestada + " en intento de autorizacion";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo ejecutar inactivacion. Cara en " +
                                PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraEncuestada + " en estado Por Autorizar";
                            bool Imprime = true;
                            bool Terminal = false;
                            string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo ejecutar activacion. Cara en " +
                                PropiedadesCara[CaraEncuestada].Estado);
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara esté Pidiendo Autorizar
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Manguera Descolgada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        //Informa cambio de estado
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                            PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.PumpControlPorAutorizar1)
                        {
                            //Pide requerimiento de autorización sólo si la cara está inicializada
                            if (PropiedadesCara[CaraEncuestada].CaraInicializada == true)
                            {
                                int IdProducto = PropiedadesCara[CaraEncuestada].ListaGrados[0].IdProducto;
                                int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                                string Lectura = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");




                                //Thread.Sleep(500);// retardo para bolivia para que el surtidor autorice llega la autorizacion muy rapido y no autorizas 08122012

                                if (!PropiedadesCara[CaraEncuestada].PeticionAutorizacion) //   Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                                {

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion1");
                                    SWRegistro.Flush();

                                    AutorizacionRequerida(CaraEncuestada, IdProducto, IdManguera, Lectura, "");

                                    PropiedadesCara[CaraEncuestada].PeticionAutorizacion = true; //  Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                                }

                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Intento de autorizacion de venta sin haber inicializado");
                                SWRegistro.Flush();
                                string Mensaje = "No se puede autorizar Cara " + CaraEncuestada + ": Favor colgar manguera";
                                bool Imprime = true;
                                bool Terminal = false;
                                string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                                ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            }
                        }

                        break;



                    /// para Bolivia se esta enviando la autorizacion antes de que suceda PumpControlPorAutorizar2 y no despachaban los surtidores 
                    case (EstadoCara.PumpControlPorAutorizar2):


                        //Informa cambio de estado
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                            PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.PumpControlPorAutorizar2)
                        {
                            //Pide requerimiento de autorización sólo si la cara está inicializada
                            //if (PropiedadesCara[CaraEncuestada].CaraInicializada == true && !PropiedadesCara[CaraEncuestada].PeticionAutorizacion)  //  Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                            if (PropiedadesCara[CaraEncuestada].CaraInicializada == true)
                            {

                                int IdProducto = PropiedadesCara[CaraEncuestada].ListaGrados[0].IdProducto;
                                int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                                string Lectura = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");


                                //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion2");
                                //SWRegistro.Flush();

                                ////Thread.Sleep(500);// retardo para bolivia para que el surtidor autorice llega la autorizacion muy rapido y no autorizas 08122012

                                //AutorizacionRequerida( CaraEncuestada,  IdProducto,  IdManguera,  Lectura);


                                //PropiedadesCara[CaraEncuestada].PeticionAutorizacion = true; //  Bandera para controla el envio del RequerirAutorizacion 07/03/2013.



                                if (!PropiedadesCara[CaraEncuestada].PeticionAutorizacion) //   Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                                {

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion2");
                                    SWRegistro.Flush();

                                    AutorizacionRequerida(CaraEncuestada, IdProducto, IdManguera, Lectura, "");

                                    PropiedadesCara[CaraEncuestada].PeticionAutorizacion = true; //  Bandera para controla el envio del RequerirAutorizacion 07/03/2013.
                                }


                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Intento de autorizacion de venta sin haber inicializado");
                                SWRegistro.Flush();
                                string Mensaje = "No se puede autorizar Cara " + CaraEncuestada + ": Favor colgar manguera";
                                bool Imprime = true;
                                bool Terminal = false;
                                string Puerto = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                                ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                            }
                        }



                        //Revisa en la matriz de Autorizacion si la venta ya esta autorizada 
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                        {
                            //Reporta lectura inicial
                            string strLecturasVolumen = PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta.ToString("N");
                            LecturaInicialVenta(CaraEncuestada, strLecturasVolumen);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Inicial venta: " + strLecturasVolumen);
                            SWRegistro.Flush();

                            /*- Evalúa si el Firmware acepta predeterminación ---------------------------------------------------------
                            // * incluido el 07/11/2008 10.10 -*/
                            //if (PropiedadesCara[CaraEncuestada].ValorPredeterminado > 0 &&
                            //    PropiedadesCara[CaraEncuestada].VersionFirmware >= Convert.ToDecimal(1.12) &&
                            //    PropiedadesCara[CaraEncuestada].VersionFirmware <= Convert.ToDecimal(1.20))

                            if (PropiedadesCara[CaraEncuestada].ValorPredeterminado > 0 &&
                          PropiedadesCara[CaraEncuestada].VersionFirmware >= Convert.ToDecimal(1.12) &&
                          PropiedadesCara[CaraEncuestada].VersionFirmware <= Convert.ToDecimal(50))//DCF 12-11-2016 PARA Loop  GST TCP-IP
                            {

                                //Ingresa en este condicional si el Firmware acepta predeterminación y hay un valor de importe a predeterminarse
                                Reintentos = 0;
                                do
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Comando Predeterminar: " +
                                    PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                                    SWRegistro.Flush();

                                    if (!ProcesoEnvioComando(ComandoSurtidor.Predeterminar))
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondió a comando Predeterminar");
                                        SWRegistro.Flush();
                                    }

                                    //ProcesoEnvioComando(ComandoSurtidor.AutorizarVenta); //DCF Al predeterminar se envia el comado de Autorizacion + el valor a predeterminar 
                                    Reintentos += 1;
                                    Thread.Sleep(20);
                                } while ((ComandoAceptado == false) && (Reintentos <= 3));

                                if (ComandoAceptado == false)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Comando Predetermina fallido. Valor: $" +
                                       PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                                    SWRegistro.Flush();
                                }

                                //DCF Solucion al error al momento de predeterminar; 
                                Reintentos = 0;
                                do
                                {
                                    Reintentos += 1;
                                    if (!ProcesoEnvioComando(ComandoSurtidor.Estado))
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Estado despues de Predeterminar");
                                        SWRegistro.Flush();
                                    }
                                } while (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.PumpControlIniciaDespacho &&
                                   PropiedadesCara[CaraEncuestada].Estado != EstadoCara.PumpControlDespacho && Reintentos <= 2);
                                //DCF
                            }
                            else
                            {
                                //Ciclo repetitivo de comando de autorización cuando el Firmware no acepta predeterminación o el valor predeterminado es 0
                                do
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Comando Autorizacion");
                                    SWRegistro.Flush();
                                    ProcesoEnvioComando(ComandoSurtidor.AutorizarVenta);
                                    Thread.Sleep(30);
                                    Reintentos += 1;
                                    if (!ProcesoEnvioComando(ComandoSurtidor.Estado))
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Estado despues de AutorizarVenta");
                                        SWRegistro.Flush();
                                    }
                                } while (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.PumpControlIniciaDespacho &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.PumpControlDespacho && Reintentos <= 2);
                            }

                            //DCF
                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inició
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.PumpControlIniciaDespacho ||
                                PropiedadesCara[CaraEncuestada].Estado == EstadoCara.PumpControlDespacho)
                            {
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Autorizacion exitosa");
                                SWRegistro.Flush();
                            }
                            /*- Incluido el 20/05/2010 ----------------------------------/
                             * Si la cara no se logró autorizar, cancela proceso de autorización
                             */
                            else
                            {
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                //PropiedadesCara[CaraEncuestada].EsVentaParcial = true;                                
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false; //DCF para no tomar lecturas cuando no se autorice la Venta. 2010/09/29

                                ProcesoEnvioComando(ComandoSurtidor.DetenerVenta);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Autorizacion Fallida.  Comando de Detencion enviado");
                                SWRegistro.Flush();
                            }
                            /*----------------------------------------------------------*/


                        }

                        // Detener Venta para que no autorice sin previo Permiso realizar esta pruebas en una estación para observa que el comportamiento del surtidor al enviar este comando
                        //{
                        //    ProcesoEnvioComando(ComandoSurtidor.DetenerVenta);
                        //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Desautorización enviada"); //DCF
                        //    SWRegistro.Flush();
                        //}


                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo TomarAccion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //CAMBIA EL PRECIO DE LA CARA
        private void CambiarPrecio()
        {
            try
            {
                //Almacena el Precio Actual de la cara en el Vector
                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                {
                    //Analiza si se debe cambiar el precio base de la cara
                    if (PropiedadesCara[CaraEncuestada].PrecioCara !=
                          PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia proceso de cambio de precio. Precio actual: " +
                        PropiedadesCara[CaraEncuestada].PrecioCara + " - Precio Nuevo: " + PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1);
                        SWRegistro.Flush();

                        int Reintentos = 0;
                        do
                        {
                            ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio);
                            Reintentos += 1;
                        } while ((ComandoAceptado == false) && (Reintentos <= 3));

                        if (ComandoAceptado == true)
                        {
                            //Comfirma que el PRECIO fue establecido exitosamente
                            if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                            {
                                //Tiempo de espera despues de cambio de precio (Recomendado por Pump Control 200ms)
                                //100ms de espera para la recepción de la respuesta cambio de precio
                                //100ms de espera para la confirmación de que el precio fue cambiado exitosamente
                                //100ms + 100ms + 20ms = 220ms        
                                if (PropiedadesCara[CaraEncuestada].PrecioCara != PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo establecer nuevo precio");
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    Thread.Sleep(20);
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Precio establecido exitosamente: " +
                                        PropiedadesCara[CaraEncuestada].PrecioCara);
                                    SWRegistro.Flush();
                                }
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Pedido Precio 2");
                                SWRegistro.Flush();
                            }
                        }
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Pedido de Precio para cambio de precio");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CambiarPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //REALIZA PROCESO DE FIN DE VENTA
        private void ProcesoFindeVenta()
        {
            try
            {
                int Reintentos = 0;
                double VolumenCalculado = 0;

                //Solamente ingresa a esta parte de código cuando no se ha inicializado la cara (inicio de programa)
                if (PropiedadesCara[CaraEncuestada].CaraInicializada == false)
                {
                    //Obtiene el factor de division
                    if (!ProcesoEnvioComando(ComandoSurtidor.Decimal))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Decimal en Fin de Venta");
                        SWRegistro.Flush();
                    }
                }

                //Si obtiene correctamente los Valores de Fin de Venta
                if (!ProcesoEnvioComando(ComandoSurtidor.DatosDespacho))
                {
                    //Si no pudo finalizar la venta correctamente, se loguea
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Datos Despacho en Proceso Fin de Venta");
                    SWRegistro.Flush();
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Obtuvo datos del despacho. Volumen: " +
                        PropiedadesCara[CaraEncuestada].Volumen + ", Importe: " + PropiedadesCara[CaraEncuestada].TotalVenta);
                    SWRegistro.Flush();

                    //PropiedadesCara[CaraEncuestada].ListaGrados[0].PresionLlenado = 0;

                    if (PropiedadesCara[CaraEncuestada].AplicaControlPresionLLenado)//DCF 08/06/2012
                    {
                        if (!ProcesoEnvioComando(ComandoSurtidor.ObtenerPresion)) // Para Chile anular este comando de Peticion OJO ***** 2011.07.21-1010
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando ObtenerPresion");
                            SWRegistro.Flush();
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Presion :" + PropiedadesCara[CaraEncuestada].ListaGrados[0].PresionLlenado.ToString("N"));
                            SWRegistro.Flush();
                        }

                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|No AplicaControlPresionLLenado");
                        SWRegistro.Flush();
                    }

                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|No se Consulta el comando ObtenerPresión");
                    //SWRegistro.Flush();

                    //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, setea bandera indicadora de Venta Finalizada
                    /*- Fecha de Inclusión: 2008/06/03 12:00 -*/
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                    /*--*/

                    if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.PumpControlFinDespacho)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Envia comando de Terminacion de Venta");
                        SWRegistro.Flush();
                        ProcesoEnvioComando(ComandoSurtidor.TerminarVenta);
                        Thread.Sleep(30);

                        if (!ProcesoEnvioComando(ComandoSurtidor.Estado))
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Estado despues de Terminar Venta.");
                            SWRegistro.Flush();
                        }
                    }

                    if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.PumpControlEspera &&
                        PropiedadesCara[CaraEncuestada].Estado != EstadoCara.FinDespachoForzado)
                    {
                        //Si hubo error en la toma del estado después de enviado el comando de fin de despacho, setea nuevamente la variable
                        /*- Fecha de Inclusión: 2009/02/03 -*/
                        PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                        /*--*/

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Estado " + PropiedadesCara[CaraEncuestada].Estado +
                            " despues de Comando Terminar Venta");
                        SWRegistro.Flush();
                    }
                    else
                    {
                        //Evalúa resultado y envía datos de fin de venta SÓLO si el estado es Espera o FinDespachoForzado
                        //Obtiene la Lectura Final de la Venta
                        PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta = 0;

                        //Toma lectura luego de asegurarse que el Estado es ESPERA
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia toma de Lecturas Finales de Venta");
                        SWRegistro.Flush();
                        if (TomarLecturas())
                        {
                            PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura;

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura Final: " +
                                PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta);
                            SWRegistro.Flush();

                            //Calcula el volumen despachado según lecturas Inicial y Final de venta
                            VolumenCalculado =
                                Convert.ToDouble(PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta -
                                PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta);
                        }
                        else
                        {
                            //Si no se pudo obtener la Lectura Final, se seteal el Volumen calculado de tal manera que no se haga nada con
                            //esa variable
                            VolumenCalculado = 0;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Falla en toma de lecturas finales de venta");
                            SWRegistro.Flush();
                        }

                        //Realiza comparación entre volumen calculado por lecturas y volumen obtenido por finalización de venta
                        // Tiene en cuenta si se reiniciaron las lecturas por secuencia normal del Totalizador del surtidor
                        if (VolumenCalculado < 0)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Lectura Final (" +
                                Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta)
                               + ") menor que Lectura Inicial (" + Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta) + ")");
                            SWRegistro.Flush();
                        }
                        else
                        {
                            //Si no se ha reiniciado el sistema, el valor de LecturaInicial es diferente de 0
                            if (PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta <= 0)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Lectura Inicial de Venta en " +
                                    PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta);
                                SWRegistro.Flush();
                            }
                            else
                            {
                                if (PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta <= 0)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Lectura Final de Venta en " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta);

                                    PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta +
                                        PropiedadesCara[CaraEncuestada].Volumen;

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura Final Calculada: " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta);

                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    /*Se compara el valor de Volumen Calculado con el valor de Volumen Recibido.
                                     * La diferencia no debe exceder el (+/-) 1%.  
                                     * Se da mayor credibilidad al calculado por lecturas*/
                                    if (PropiedadesCara[CaraEncuestada].Volumen < Convert.ToDecimal(VolumenCalculado * 0.99) ||
                                        PropiedadesCara[CaraEncuestada].Volumen > Convert.ToDecimal(VolumenCalculado * 1.01))
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Volumen Reportado por surtidor (" +
                                            Convert.ToString(PropiedadesCara[CaraEncuestada].Volumen) +
                                            ") no corresponde con diferencia de lecturas (" + Convert.ToString(VolumenCalculado) + ")");
                                        SWRegistro.Flush();
                                        if (PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta == PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta)
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura Inicial igual a Lectura Final: " +
                                               PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta);
                                            SWRegistro.Flush();
                                            PropiedadesCara[CaraEncuestada].Volumen = 0;
                                            PropiedadesCara[CaraEncuestada].TotalVenta = 0;
                                        }
                                        /*- Fecha de Inclusión: 2008/03/06 17:40 -*/
                                        //Si el volumen de venta se recibió en 0, coloca el calculo
                                        else if (PropiedadesCara[CaraEncuestada].Volumen == 0)
                                        {
                                            PropiedadesCara[CaraEncuestada].Volumen = Convert.ToDecimal(VolumenCalculado);
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso| Volumen recibido en Cero. Nuevo volumen calculado = " +
                                                PropiedadesCara[CaraEncuestada].Volumen);
                                            SWRegistro.Flush();
                                        }
                                        /*--*/
                                    }
                                }
                            }
                        }

                        //Si se realizó una venta con valores de m3 y $ mayor que cero
                        if (PropiedadesCara[CaraEncuestada].Volumen == 0)
                        {
                            // VentaInterrumpidaEnCero( CaraEncuestada);

                            if (VentaInterrumpidaEnCero != null)
                            {
                                VentaInterrumpidaEnCero(CaraEncuestada);
                            }
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Venta en CERO");
                            SWRegistro.Flush();
                        }
                        else
                        {
                            //Obtiene el precio con que se realizó la venta
                            PropiedadesCara[CaraEncuestada].PrecioCara = 0;
                            Reintentos = 0;
                            do
                            {
                                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Obtiene el precio de venta: " +
                                        PropiedadesCara[CaraEncuestada].PrecioCara);
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a Pedido de Precio para Proceso Fin de Venta");
                                    SWRegistro.Flush();
                                }
                                Reintentos += 1;

                            } while (PropiedadesCara[CaraEncuestada].PrecioCara == 0 && Reintentos <= 3);

                            if (PropiedadesCara[CaraEncuestada].PrecioCara == 0)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Precio obtenido en 0");
                                SWRegistro.Flush();

                                //Asigna precio de Base de Datos (2008/02/27)
                                PropiedadesCara[CaraEncuestada].PrecioCara = PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1;
                            }

                            if (PropiedadesCara[CaraEncuestada].TotalVenta == 0)
                            {
                                PropiedadesCara[CaraEncuestada].TotalVenta = PropiedadesCara[CaraEncuestada].Volumen * PropiedadesCara[CaraEncuestada].PrecioCara;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Importe recibido en Cero. Total Importe Calculado: " +
                                    Convert.ToString(PropiedadesCara[CaraEncuestada].TotalVenta));
                                SWRegistro.Flush();
                            }

                            //Dispara evento al programa principal si la venta es diferente de 0
                            string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N2");
                            string strPrecio = PropiedadesCara[CaraEncuestada].PrecioCara.ToString("N3");
                            string strLecutraFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta.ToString("N");
                            string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                            string Presion = PropiedadesCara[CaraEncuestada].ListaGrados[0].PresionLlenado.ToString("N");
                            int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                            byte bytProducto = Convert.ToByte(PropiedadesCara[CaraEncuestada].ListaGrados[0].IdProducto);
                            string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta.ToString("N3");

                            //Loguea evento Fin de Venta
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fin de venta: Total Venta: " + strTotalVenta +
                                " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecutraFinalVenta + " - Volumen: " + strVolumen + " - Presion: " + Presion);
                            SWRegistro.Flush();

                            VentaFinalizada(CaraEncuestada, strTotalVenta, strPrecio, strLecutraFinalVenta, strVolumen, Convert.ToString(bytProducto), IdManguera, Presion, strLecturaInicialVenta);

                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.PumpControlEspera)
                            {
                                int mangueraColgada = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                                CaraEnReposo(CaraEncuestada, mangueraColgada);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara en Espera");
                                SWRegistro.Flush();

                                //Reset del elemento que indica que la Cara debe ser autorizada //DCF
                                if (PropiedadesCara[CaraEncuestada].AutorizarCara)
                                {
                                    PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara Desautorizada.");
                                    SWRegistro.Flush();
                                }



                                ////DCF 15-09-2015 pruebas IMW
                                //if (!ProcesoEnvioComando(ComandoSurtidor.DetenerVenta))
                                //{
                                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando DetenerVenta");
                                //    SWRegistro.Flush();
                                //}
                                //else
                                //{
                                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento| comando DetenerVenta OK");
                                //    SWRegistro.Flush();
                                //}


                            }
                            //La lectura inicial de la venta siguiente se asume como lectura final de esta venta
                            PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[0].LecturaFinalVenta;
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ProcesoFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //PARA TOMAR LECTURAS DE APERTURA Y/O CIERRE DE TURNO
        private void LecturaAperturaCierre()
        {
            try
            {
                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();
                System.Array LecturasEnvio;

                //Si la Lectura que se va a tomar es la FINAL de turno
                if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia toma de lecturas para Cierre de Turno");
                    SWRegistro.Flush();

                    if (TomarLecturas())
                    {

                        //Almacena las lecturas en la lista
                        ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura));
                        LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                        ArrayLecturas.CopyTo(LecturasEnvio);



                        string strLecturaTurno = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura.ToString("N");
                        //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                        LecturaTurnoCerrado(LecturasEnvio);
                        PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa Lectura Final Turno: " + strLecturaTurno);
                        SWRegistro.Flush();

                    }
                    else
                    {
                        //Envía ERROR EN TOMA DE LECTURAS, si la respuesta recibida al comando Lectura no fue satisfactoria
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Error en Toma de Lecturas Finales de Turno";
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                            CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Informa fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                            SWRegistro.Flush();

                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                    }
                }
                //Si la Lectura que se va a tomar es la INICIAL de turno
                else if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia toma de lecturas para Apertura de Turno");
                    SWRegistro.Flush();

                    if (TomarLecturas())
                    {
                        //Almacena las lecturas en la lista
                        ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura));
                        LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                        ArrayLecturas.CopyTo(LecturasEnvio);

                        string strLecturaTurno = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura.ToString("N");
                        //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                        //LecturaTurnoAbierto( CaraEncuestada,  strLecturaTurno);
                        LecturaTurnoAbierto(LecturasEnvio);


                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Inicial Turno: " + strLecturaTurno);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                        //Si hay cambio de precio pendiente (precio base: PrecioNivel1), lo aplica
                        if (PropiedadesCara[CaraEncuestada].PrecioCara !=
                            PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1)
                        {
                            //SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicia proceso de cambio de precio");
                            //SWRegistro.Flush();
                            CambiarPrecio();
                        }
                        //    //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                        //if (PrecioEDS != PropiedadesCara[CaraEncuestada].PrecioCara)
                        //    CambiarPrecio();
                    }
                    else
                    {
                        //Envía ERROR EN TOMA DE LECTURAS, si la respuesta recibida al comando Lectura no fue satisfactoria
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Error en toma de Lecturas Iniciales de Turno";
                            bool EstadoTurno = false;
                            PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Iniciales. " + MensajeErrorLectura);
                            SWRegistro.Flush();

                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ENVIA COMANDO DE TOMA DE LECTURAS Y LANZA ENVENTO PARA REPORTAR LECTURAS AL SERVICIO WINDOWS
        private bool TomarLecturas()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                bool TomaLecturasExitoso = false;

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    Reintentos += 1;
                    if (ProcesoEnvioComando(ComandoSurtidor.Totales))
                    {
                        TomaLecturasExitoso = true;
                        if (PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura == 0)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura recibida en CERO");
                            SWRegistro.Flush();
                        }
                        break;
                    }
                    else
                    {
                        TomaLecturasExitoso = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Totales");
                        SWRegistro.Flush();
                    }
                } while (Reintentos <= 3);

                if (Reintentos >= 2)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|" + Reintentos + " intentos de toma de lectura ");
                    SWRegistro.Flush();
                }

                //Si el proceso de toma de lecturas fue exitoso, devuelve True
                return TomaLecturasExitoso;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo TomarLecturas: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        private bool TomarLecturaActivacionCara()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;

                //Realiza hasta tres reintentos de toma de lecturas
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia proceso de toma de lecturas para Activacion Cara");
                SWRegistro.Flush();
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.Totales))
                    {
                        ////////////////////////////////////////////////////////////////////
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio a comando Totales para Activacion de Cara");
                        SWRegistro.Flush();
                        ////////////////////////////////////////////////////////////////////
                        //Si el proceso no fue exitoso, la función devuelve False
                        return false;
                    }
                    else if (PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura == 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Lectura recibida en CERO");
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura activacion: " + PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura);
                        SWRegistro.Flush();
                    }
                } while (PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura == 0 && Reintentos <= 3);
                try
                {
                    ArrayLecturas = new System.Collections.ArrayList();
                    ArrayLecturas.Add(CaraEncuestada + "|" + PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura);
                }
                catch (Exception Excepcion)
                {
                    string MensajeExcepcion = "Excepcion en el Metodo TomarLecturaActivacionCara: ArrayLis " + Excepcion;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                    SWRegistro.Flush();
                }

                //Si el proceso de toma de lecturas fue exitoso, devuelve True
                return true;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo TomarLecturaActivacionCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }



        #endregion

        #region METODOS AUXILIARES

        //ANALIZA LA INTEGRIDAD Y CORRESPONDENCIA DE LA TRAMA RESPUESTA
        private bool ComprobacionTramaRx()
        {
            try
            {
                //Inicializa bandera que indica que hubo error en trama
                bool ErrorEnTrama = true;

                //Obtiene el CRC, número de Surtidor y la Longitud que viene en la trama
                byte CRCRx = TramaRx[TramaRx.Length - 1];
                byte SurtidorRx = TramaRx[1];
                int LongitudTramaRx = Convert.ToInt16(TramaRx[2]);

                //Calcula el CRC, el Surtidor y la Longitud correspondiente al mensaje recibido, Cara encuestada y Longitud de la trama
                byte CRC = ObtenerCRC(TramaRx);
                byte Surtidor = Convert.ToByte(ObtenerSurtidor(CaraEncuestada));
                int LongitudTrama = TramaRx.Length;

                //1º. Evalúa Byte de Redundancia Cíclica
                if (CRCRx == CRC)
                {
                    //2º. Evalúa Surtidor que responde
                    if (SurtidorRx == Surtidor)
                    {
                        //3o. Evalúa la longitud de la trama
                        if ((LongitudTrama - 3) == (LongitudTramaRx))
                            ErrorEnTrama = false;
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Longitud recibida: " + (LongitudTramaRx) +
                                " - Longitud Real: " + (LongitudTrama - 3));
                            SWRegistro.Flush();
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Surtidor encuestado: " + Surtidor +
                            " - Surtidor que responde: " + SurtidorRx);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Checksum recibido: " + CRCRx +
                        " - Checksum real: " + CRC);
                    SWRegistro.Flush();
                }
                return !ErrorEnTrama;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ComprobacionTramaRx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //CALCULA EL NUMERO DEL SURTIDOR A ENCUESTAR A PARTIR DE LA CARA
        private int ObtenerSurtidor(int Cara)
        {
            try
            {
                int Surtidor;
                if ((CaraEncuestada % 2) != 0)
                    Surtidor = (CaraEncuestada + 1) / 2;
                else
                    Surtidor = CaraEncuestada / 2;
                return Surtidor;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ObtenerSurtidor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 0;
            }
        }

        //CALCULA EL CARACTER DE REDUNDANCIA CICLICA
        private byte ObtenerCRC(byte[] Trama)
        {
            try
            {
                byte CRC = new byte();
                CRC = Trama[0];
                int j;
                for (j = 1; j <= Trama.Length - 2; j++)
                {
                    CRC = Convert.ToByte(Convert.ToInt16(CRC) ^ Convert.ToInt16(Trama[j]));
                }
                return CRC;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ObtenerCRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 0;
            }
        }

        //CALCULA LOS VALORES EN LOS DATOS DE LA TRAMA RESPUESTA
        private decimal ObtenerValor(byte[] Trama, int PosicionInicio, int Longitud)
        {
            try
            {
                double Valor = 0;
                int Potencia = 0;
                for (int i = PosicionInicio; i <= (PosicionInicio + Longitud - 1); i++)
                {
                    Valor += Trama[i] * Math.Pow(256, Potencia);
                    Potencia += 1;
                }
                /*Respuesta	AA 02 09 77 00 00 00 C2 03 00 00 17
                 * Volumen: 0x77 0x00 0x00 0x00 =  0x77 = 1.19 m3
                 * Total:   0xC2 0x03 0x00 0x00 = 0x3C2 = $ 962 */
                return Convert.ToDecimal(Valor);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ObtenerValor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 0;
            }
        }

        //INICIALIZA VALORES DE LA MATRIZ PARA TOMA DE LECTURAS
        private void IniciaTomaLecturasTurno(string Surtidores, bool Apertura)
        {
            try
            {
                string[] bSurtidores = Surtidores.Split('|');
                byte Cara;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        Cara = Convert.ToByte(Convert.ToByte(bSurtidores[i]) * 2 - 1);

                        //Si la cara esta en la red  
                        if (PropiedadesCara.ContainsKey(Cara))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[Cara].FalloTomaLecturaTurno = false;

                            if (Apertura)
                                PropiedadesCara[Cara].TomarLecturaAperturaTurno = true;   //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                            else
                                PropiedadesCara[Cara].TomarLecturaCierreTurno = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        Cara = Convert.ToByte(Convert.ToByte(bSurtidores[i]) * 2);

                        //Si la cara esta en la red  
                        if (PropiedadesCara.ContainsKey(Cara))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[Cara].FalloTomaLecturaTurno = false;

                            if (Apertura)
                                PropiedadesCara[Cara].TomarLecturaAperturaTurno = true;   //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                            else
                                PropiedadesCara[Cara].TomarLecturaCierreTurno = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo IniciaTomaLecturasTurno: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public static void CallBackMethod(IAsyncResult asyncresult)
        {

        }

        #endregion

        #region EVENTOS DE LA CLASE
        public void Evento_ProgramarCambioPrecioKardex(byte Cara, string Valor)
        {
            try
            {
                PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1 = Convert.ToDecimal(Valor);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_CambioPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
        
        {
            try
            {
                //Revisa si la cara pertenece a esta red
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    //Bandera que indica que la cara debe autorizarse para desapchar
                    PropiedadesCara[Cara].AutorizarCara = true;

                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                        " - Tipo de Programacion: " + TipoProgramacion);
                    SWRegistro.Flush();

                    PropiedadesCara[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    //Si viene valor para predeterminar setea banderas
                    if (PropiedadesCara[Cara].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            PropiedadesCara[Cara].PredeterminarVolumen = true;
                            PropiedadesCara[Cara].PredeterminarImporte = false;
                        }
                        else
                        {
                            PropiedadesCara[Cara].PredeterminarVolumen = false;
                            PropiedadesCara[Cara].PredeterminarImporte = true;
                        }
                    }
                    else
                    {
                        PropiedadesCara[Cara].PredeterminarVolumen = false;
                        PropiedadesCara[Cara].PredeterminarImporte = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_VentaAutorizada: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //private void oEvento_TurnoAbierto( string Surtidores,  string PuertoTerminal,  System.Array Precios)      
        // {
        //    try
        //    {
        //        IniciaTomaLecturasTurno(Surtidores, true);  //Indica que las lecturas a tomar son las iniciales 
        //        PrecioEDS = Convert.ToDecimal(Precio);  //Asigna el nuevo precio           
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepcion en el Evento oEvento_TurnoAbierto: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
        //        SWRegistro.Flush();
        //    }
        //}

        public void Evento_TurnoAbierto(string Surtidores, string PuertoTerminal, System.Array Precios)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|Evento Recibido|(TurnoAbierto). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Diccionario de los productos manejados
                Dictionary<int, Producto> Productos = new Dictionary<int, Producto>();

                //Ciclo que arma Diccionario de Productos
                foreach (string sPreciosProducto in Precios)
                {
                    //Objeto Producto para añadir al Diccionario
                    Producto PrecioProducto = new Producto();

                    string[] vPreciosProducto = sPreciosProducto.Split('|');
                    PrecioProducto.IdProducto = Convert.ToByte(vPreciosProducto[0]);
                    PrecioProducto.PrecioNivel1 = Convert.ToDecimal(vPreciosProducto[1]);
                    PrecioProducto.PrecioNivel2 = Convert.ToDecimal(vPreciosProducto[2]);

                    //Si el prodcuto no existe dentro del diccionario, lo añade
                    if (!Productos.ContainsKey(PrecioProducto.IdProducto))
                        Productos.Add(PrecioProducto.IdProducto, PrecioProducto);
                    else
                    {
                        Productos[PrecioProducto.IdProducto].IdProducto = PrecioProducto.IdProducto;
                        Productos[PrecioProducto.IdProducto].PrecioNivel1 = PrecioProducto.PrecioNivel1;
                        Productos[PrecioProducto.IdProducto].PrecioNivel2 = PrecioProducto.PrecioNivel2;
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

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraLectura].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel1 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel1;
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel2 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel2;
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraLectura].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel1 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel1;
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel2 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel2;
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_TurnoCerrado(string Surtidores, string PuertoTerminal)
        {
            try
            {
                IniciaTomaLecturasTurno(Surtidores, false); //Indica que las lecturas a tomar son las finales             
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto)
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    PropiedadesCara[Cara].InactivarCara = true;
                    PropiedadesCara[Cara].PuertoParaImprimir = Puerto;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Comando para Inactivar");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_InactivarCaraCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEvento_CambiarDensidad(string predDensidad)
        {
            try
            {
                foreach (RedSurtidor Propiedad in PropiedadesCara.Values)
                {
                    PropiedadesCara[Propiedad.Cara].CambiarDensidad = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Propiedad.Cara + "|Evento|Recibe comando de cambio de Densidad: " + predDensidad + ". Comando para Cambiar Densidad");
                    SWRegistro.Flush();
                }
                DensidadEDS = Convert.ToDecimal(predDensidad);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_CambiarDensidad: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                //byte Cara = 1;
                foreach (RedSurtidor Propiedad in PropiedadesCara.Values)
                    PropiedadesCara[Propiedad.Cara].CambiarDensidad = false;
            }
        }
        public void Evento_FinalizarCambioTarjeta(byte Cara)
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    PropiedadesCara[Cara].ActivarCara = true;
                    PropiedadesCara[Cara].Activa = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe comando para activacion");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_FinalizarCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
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
                    SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(Cara) + "|Evento|oEvento_FinalizarVentaPorMonitoreoCHIP|Solicitar detener venta");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_FinalizarVentaPorMonitoreoCHIP: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }
        public void Evento_CerrarProtocolo()
        {
            this.CondicionCiclo = false;
        }
        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)//Realizado por el remplazo del shared event, usa una interfaz en el proyecto Fabrica Protocolo
        {
        }
        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
        }
        public void Evento_CancelarVenta(byte Cara)
        {

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

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura Antesde  .Activa:::::::|...... " + CaraLectura);
                    SWRegistro.Flush();


                    //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                    CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                    if (PropiedadesCara.ContainsKey(CaraTmp))
                    {

                    //Si la cara esta activa se solicita la toma de lecturas en la apertura
                    if (PropiedadesCara[CaraLectura].Activa)//DCF19/01/2018
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura despues Activa//////// " + CaraLectura);
                        SWRegistro.Flush();  

                      

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura despues CaraTmp **********  " + CaraLectura);
                            SWRegistro.Flush();   

                            if (PropiedadesCara[CaraTmp].Estado == EstadoCara.PumpControlEspera)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {


                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();
                                while (!EncuentaFinalizada)
                                {
                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Esperando fin de encuesta");
                                    //SWRegistro.Flush();
                                    // System.Threading.Thread.Sleep(200);

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))   
                                }

                                CaraEncuestada = CaraTmp;
                                CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias                            

                                TomarLecturas(); // obtener las lecturas de la cara en cuestion 

                                int i;
                                for (i = 0; i <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; i++)
                                {
                                    Lecturas += (Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD) + "|" +
                                    Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[i].Lectura) + "|");

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                        PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD + " - Lectura " +
                                        PropiedadesCara[CaraTmp].ListaGrados[i].Lectura);
                                    SWRegistro.Flush();
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
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|Cara No Activa");//DCF19/01/2018
                            SWRegistro.Flush();
                        }

                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: SolicitarLecturasSurtidor");
                        SWRegistro.Flush();

                        Lecturas = "E_ Cara fuera de red";
                    }


                    //Organiza banderas de pedido de lecturas para la cara PAR
                    CaraLectura = Convert.ToByte(Convert.ToInt16(Surtidor) * 2);

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura|...... " + CaraLectura);
                    SWRegistro.Flush();


                //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                if (PropiedadesCara.ContainsKey(CaraTmp))
                {
                    //Si la cara esta activa se solicita la toma de lecturas en la apertura
                    if (PropiedadesCara[CaraLectura].Activa)//DCF19/01/2018
                    {
                       
                            if (PropiedadesCara[CaraTmp].Estado == EstadoCara.PumpControlEspera)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {                                
                           
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();

                                while (!EncuentaFinalizada)
                                {
                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Esperando fin de encuesta");
                                    //SWRegistro.Flush();
                                    //System.Threading.Thread.Sleep(200);

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                }


                                CaraEncuestada = CaraTmp;
                                CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                                TomarLecturas(); // obtener las lecturas de la cara en cuestion 


                                int i;
                                for (i = 0; i <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; i++)
                                {
                                    Lecturas += (Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD) + "|" +
                                    Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[i].Lectura) + "|");

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                        PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD + " - Lectura " +
                                        PropiedadesCara[CaraTmp].ListaGrados[i].Lectura);
                                    SWRegistro.Flush();
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
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|Cara No Activa");//DCF19/01/2018
                            SWRegistro.Flush();
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: SolicitarLecturasSurtidor");
                        SWRegistro.Flush();

                        Lecturas = "E_ Cara fuera de red";
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


        public byte ConvertirCaraBD(byte caraBD) //YEZID Alias de las caras //DCF 2011-05-14
        {
            byte CaraSurtidor = 0;
            try
            {
                foreach (RedSurtidor ORedCaras in  PropiedadesCara.Values)
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


        #endregion
    }
}