using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto 
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using POSstation.Protocolos;


namespace POSstation.Protocolos
{
    public class Compac:iProtocolo
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


        //REGION DE DECLARACIONES
        #region DECLARACION DE VARIABLES Y DEFINICIONES


        System.Globalization.CultureInfo cultura;
        //VARIABLES DE CONTROL
        ComandoSurtidor ComandoCaras;   //Arreglo que almacena el COMANDO enviado al surtidor (Vector organizado por caras ascendentemente)
        byte CaraEncuestada;            //Cara que se esta ENCUESTANDO
        byte NumerodeCaras;             //Almacena la cantidad de caras a encuestar
        byte CaraInicial;               //Almacena la CARA INICIAL de la red de surtidores Gilbarco        
        decimal PrecioEDS;				//Almacena el PRECIO vigente en la EDS
        //decimal Lectura;                 //Almacena el valor de la LECTURA tomada
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int BytesEsperados;             //Declara la cantidad de bytes esperados por Comando
        int eco;
        bool CondicionCiclo = true;  


        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO        

        //ENUMERACIONES UTILIZADA PARA CREAR VARIABLES
        //Define los posibles ESTADOS de cada una de las Caras del Surtidor
        //public enum EstadoCara
        //{
        //    Indeterminado = 0x00,
        //    Espera = 0x30,
        //    Autorizada = 0x31,
        //    Pre_Despacho = 0x32,
        //    Despachando = 0x33,
        //    FlujoLento = 0x34,
        //    FinalizandoDespachoPredeterminado = 0x35,
        //    FinalizandoDespachoNozzle = 0x36,
        //    DespachoEnCero = 0x37,
        //    FinDespachoPredeterminado = 0x38,
        //    CambioPrecioEnProceso = 0x39,
        //    FinDespacho = 0x3A,
        //    StartUp = 0x3F,
        //    Error = 0x65,
        //    CambiadoFactorK = 0x3E,
        //    IdentificadorDisponible = 0x3D,
        //    BadCard = 0x21,
        //    PinpadAvailable = 0x64 
        //}

        //Define los dos Estados Físicos Posibles de la manguera
        //public enum Idle
        //{
        //    Indeterminado,
        //    Extraida,
        //    Colgada
        //}

        //Define los Posibles Comandos a la Cara
        public enum ComandoSurtidor
        {
            Autorizar,                  // Lock: 1-Authorize
            Reautorizar,                // Lock: 5-Clear temporary stop
            TerminarVenta,              // Lock: 6-Clear End of Delivery (Status '8' or ':')
            InicializarCara,            // Lock: 9-Clear StartUp/Initialise (Status 15)
            Estado,                     /* Lock: 3-Hold o 4-Temporary Stop
                                         * Poll: Q-Request Status - No data*/
            EnviarValorProgramado,      // Poll: $-Send prepay amount
            ObtenerDatosDespacho,       // Poll: D-Request delivery amount and quantity
            EnviarPrecio,               // Poll: G-Send price (single host)
            ObtenerPrecio,              // Poll: C-Request price (single host)
            ObtenerCodigoError,         // Poll: E-Get dispenser error code
            ObtenerTotalizadores,       // Poll: a-Request electronic totals
            ObtenerVersionFirmware,     // Poll: j-Get software version
            ObtenerPosicionDecimal      // Poll: o-Get configuration data
        }

        //ARREGLOS DE INFORMACION NECESARIA POR CARA
        EstadoCara[] EstadoActual;      //Arreglo que almacena el ESTADO de cada una de las Caras (Vector organizado por caras ascendentemente)
        EstadoCara[] EstadoAnterior;    //Arreglo que almacena el ESTADO inmediatamente ANTERIOR de cada una de las Caras (Vector organizado por caras ascendentemente)
        //Idle[] IdleActual;              //Arreglo que almacena el ESTADO físico de la manguera
        //Idle[] IdleAnterior;            //Arreglo que almacena el ESTADO físico de la manguera (inmediatamente anterior)
        //bool[] PredeterminarVolumen;    //Determina el tipo de PRESET para la autorizacion de la venta
        //bool[] PredeterminarValor;      //Determina si el predeterminado es de Valor
        //bool[] CaraInicializada;	    //Determina si la cara ya fue inicializada
        //bool[] AutorizarCara;           //Determina si la cara debe autorizarse
        //bool[] TomarLecturaApertura;    //Determina si deben tomarse las lecturas para Apertura de Turno
        //bool[] TomarLecturaCierre;      //Determina si deben tomarse las lecturas para Cierre de Turno
        //bool[] FalloComunicacionReportado; //Indica si un nuevo error de comunicacion fue reportado
        //bool[] FalloTomaLecturas;       //Variable que controla que la toma de lecturas fue adecuada
        //bool[] Autorizando;             //Variable que indica que la venta está autorizada por el AUTORIZADOR
        //string[] Puerto;
        string PuertoSurtidores;

        //decimal[] Importe;              //Almacena valores parciales y finales de dinero
        //decimal[] Volumen;              //Almacena valores parciales y finales de volumen
        //decimal[] Precio;		        //Almacena el PRECIO ACTUAL de la cara
        //decimal[] LecturaInicialVenta;   //Almacena la LECTURA INICIAL de cada venta en curso
        //decimal[] LecturaFinalVenta;     //Almacena la LECTURA FINAL de cada venta en curso                
        //decimal[] ValorPredeterminado;   //Almacena el valor de PRESET para la autorizacion de la venta

        //*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        //bool[] FalloComunicacion;      //Almacena el tipo de fallo de comunicacion      
  
        bool FalloComunicacion;             //Establece si hubo error en la comunicación (Surtidor no contesta)

        /*Tramas compuestas de bytes para comunicacion con SURTIDOR */
        char[] TramaRx = new char[1];   //Almacena la TRAMA RECIBIDA
        char[] TramaTx = new char[1];   //Almacena la TRAMA A ENVIAR       

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        System.Timers.Timer PollingTimer = new System.Timers.Timer(20); //Definicion del TIMER DE ENCUESTA
        // SharedEventsFuelStation.CMensaje oEvento;                                 //Controla la comunicacion entre las aplicaciones por medio de eventos        

        //Diccionario donde se almacenan las Caras y sus propiedades
        Dictionary<byte, RedSurtidor> EstructuraRedSurtidor;

        //VARIABLES VARIAS
        //Instancia Arreglo de lecturas para reportar reactivación de cara
        System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

        //List<Cara> EstructuraRedSurtidor = new List<Cara>();

        //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        string ArchivoTramas;
         string ArchivoRegistroSucesos;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWTramas;

        //Variable que almacen la ruta y el nombre del archivo que guarda inconsistencias en el proceso logico
        //string ArchivoRegistro;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWRegistro;

        string COM_Puerto;
        #endregion

        #region METODOS PRINCIPALES


        private void validarCultura()
        {
            if (!cultura.Equals(Thread.CurrentThread.CurrentUICulture))
            {
                Thread.CurrentThread.CurrentUICulture = cultura;
            }
        }

        //PUNTO DE ARRANQUE DE LA CLASE       
       public Compac(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
               

                COM_Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }


                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Compac-Trama (" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Compac-Registro (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                //Escribe encabezado
                SWRegistro.WriteLine("===================|==|======|===================================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 08-03-2012 0900"); //DCF control del tamaño de los archivos de logueo Sucesos y Tramas.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 14-05-2012 1201"); //DCF 14-05-2012 se iguala la lectura final de la venta anterior a la lectura inicial de la venta en curso, en caso de fallo en la toma de lectura inicial.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 02-06-2012 0913"); //Se borra la lectura inicial para que no sea utilizada en la próxima venta. DCF 0206-2012 
                //SWRegistro.WriteLine("Numero de Caras: " + NumerodeCaras + " - Cara Inicial: " + CaraInicial + " - Precio: " + strPrecioEDS);

                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 05-07-2012 1259"); //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 09-07-2012 1019"); //
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 28-11-2013 1023"); //Environment.CurrentDirectory  por  Application.StartupPath 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 10-04-2018 1751");//DCF 10/04/2018
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 11-04-2018 1613"); //DCF 11/04/2018
                SWRegistro.WriteLine("===================|==|======|===================================================");
                SWRegistro.Flush();





                ////Crea archivos de texto para logueo
                //ArchivoRegistro = Application.ExecutablePath + "Compac-Registro (" + Puerto + ").txt";
                //SWRegistro = File.AppendText(ArchivoRegistro);

                ////Escribe encabezado
                //SWRegistro.WriteLine();
                //SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.WriteLine(DateTime.Now);
                //SWRegistro.WriteLine("COMPAC");
                //SWRegistro.WriteLine("Numero de Caras: " + NumerodeCaras + " - Cara Inicial: " + CaraInicial + " - Precio: " + strPrecioEDS);
                //SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.Flush();

                //ArchivoTramas = Application.ExecutablePath + "Compac-Trama (" + Puerto + ").txt";
                //SWTramas = File.AppendText(ArchivoTramas);

                ////Escribe encabezado
                //SWTramas.WriteLine();
                //SWTramas.WriteLine("=====================================================================");
                //SWTramas.WriteLine(DateTime.Now);
                //SWTramas.WriteLine("COMPAC");
                //SWTramas.WriteLine("Archivo de Tramas transmitidas y Tramas Recibidas");
                //SWTramas.WriteLine("=====================================================================");
                //SWTramas.Flush();

                //Instancia los eventos de los objetos Timer
                //PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);

                ////Instancia los eventos disparados por la aplicacion cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);

                ////Instancia los eventos disparados por la aplicación cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                ////oEvento.CambioPrecio += new SharedEventsFuelStation.__CMensaje_CambioPrecioEventHandler(oEvento_CambioPrecio);
                //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                ////oEvento.InactivarCaraCambioTarjeta += new SharedEventsFuelStation.__CMensaje_InactivarCaraCambioTarjetaEventHandler(oEvento_InactivarCaraCambioTarjeta);
                ////oEvento.FinalizarCambioTarjeta += new SharedEventsFuelStation.__CMensaje_FinalizarCambioTarjetaEventHandler(oEvento_FinalizarCambioTarjeta);
                ////oEvento.CambiarDensidad += new SharedEventsFuelStation.__CMensaje_CambiarDensidadEventHandler(oEvento_CambiarDensidad);
                ////oEvento.FinalizarVentaPorMonitoreoCHIP += new SharedEventsFuelStation.__CMensaje_FinalizarVentaPorMonitoreoCHIPEventHandler(oEvento_FinalizarVentaPorMonitoreoCHIP);
                //oEvento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEvento_CerrarProtocolo);
                //oEvento.ProgramarCambioPrecioKardex += new SharedEventsFuelStation.__CMensaje_ProgramarCambioPrecioKardexEventHandler(oEvento_ProgramarCambioPrecioKardex);
               


                ////Almacena el numero de caras a encuestarse
                //this.NumerodeCaras = Convert.ToByte(NumerodeCaras + CaraInicial - 1);
                //this.CaraInicial = CaraInicial;

                //EstructuraRedSurtidor = PropiedadCara;

                //Almacena el Precio de Venta establecido para la EDS
                //PrecioEDS = Convert.ToDecimal(strPrecioEDS);

                PuertoSurtidores = Puerto;
                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 2400;
                    PuertoCom.DataBits = 7;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
                    PuertoCom.ReadBufferSize = 128;
                    PuertoCom.WriteBufferSize = 128;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception ex)
                    {
                        throw ex; //throw new Exception ("Comunicacion con surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }


                //Armar diccionario
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;


                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica                
                HiloCicloCaras.Start();

                ////Se dimensiona cada uno de los arreglos utilizados
                //EstadoActual = new EstadoCara[this.NumerodeCaras];
                //EstadoAnterior = new EstadoCara[this.NumerodeCaras];
                //IdleActual = new Idle[this.NumerodeCaras];
                //IdleAnterior = new Idle[this.NumerodeCaras];

                //PredeterminarVolumen = new bool[this.NumerodeCaras];
                //PredeterminarValor = new bool[this.NumerodeCaras];
                //CaraInicializada = new bool[this.NumerodeCaras];
                //AutorizarCara = new bool[this.NumerodeCaras];
                //TomarLecturaApertura = new bool[this.NumerodeCaras];
                //TomarLecturaCierre = new bool[this.NumerodeCaras];
                //FalloComunicacionReportado = new bool[this.NumerodeCaras];
                //FalloTomaLecturas = new bool[this.NumerodeCaras];
                //Autorizando = new bool[this.NumerodeCaras];

                //Precio = new decimal[this.NumerodeCaras];
                //LecturaInicialVenta = new decimal[this.NumerodeCaras];
                //LecturaFinalVenta = new decimal[this.NumerodeCaras];
                //ValorPredeterminado = new decimal[this.NumerodeCaras];
                //Volumen = new decimal[this.NumerodeCaras];
                //Importe = new decimal[this.NumerodeCaras];

                //this.Puerto = new string[this.NumerodeCaras];

                //FalloComunicacion = new bool[2];
                ///* [0] Error en Datos
                // * [1] Error en Comunicación: Trama incompleta o no hay respuesta del surtidor*/

                ////Establece inicio de encuesta en Cara Inicial
                //CaraEncuestada = CaraInicial;





                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraInicial + "|Proceso|Cara Inicial");
                SWRegistro.Flush();

                //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
                //PollingTimer.AutoReset = true;                

                //Se activa el timer por primera vez
                //PollingTimer.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Compac";
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + this.NumerodeCaras + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public Compac(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco,  System.Globalization.CultureInfo miCultura)
        {
            try
            {
                cultura = miCultura;

                COM_Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }


                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Compac-Trama (" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Compac-Registro (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                //Escribe encabezado
                SWRegistro.WriteLine("===================|==|======|===================================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 08-03-2012 0900"); //DCF control del tamaño de los archivos de logueo Sucesos y Tramas.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 14-05-2012 1201"); //DCF 14-05-2012 se iguala la lectura final de la venta anterior a la lectura inicial de la venta en curso, en caso de fallo en la toma de lectura inicial.
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 02-06-2012 0913"); //Se borra la lectura inicial para que no sea utilizada en la próxima venta. DCF 0206-2012 
                //SWRegistro.WriteLine("Numero de Caras: " + NumerodeCaras + " - Cara Inicial: " + CaraInicial + " - Precio: " + strPrecioEDS);

                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 05-07-2012 1259"); //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 10-07-2012 0602"); //
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 11-21-2012 1551"); //|Excepcion|CondicionCiclo:
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 28-11-2013 1023"); //Environment.CurrentDirectory  por  Application.StartupPath 
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 10-04-2018 1751");//DCF 10/04/2018
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo COMPAC modificado 11-04-2018 1613"); //DCF 11/04/2018
                SWRegistro.WriteLine("===================|==|======|===================================================");
                SWRegistro.Flush();





                ////Crea archivos de texto para logueo
                //ArchivoRegistro = Application.ExecutablePath + "Compac-Registro (" + Puerto + ").txt";
                //SWRegistro = File.AppendText(ArchivoRegistro);

                ////Escribe encabezado
                //SWRegistro.WriteLine();
                //SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.WriteLine(DateTime.Now);
                //SWRegistro.WriteLine("COMPAC");
                //SWRegistro.WriteLine("Numero de Caras: " + NumerodeCaras + " - Cara Inicial: " + CaraInicial + " - Precio: " + strPrecioEDS);
                //SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.Flush();

                //ArchivoTramas = Application.ExecutablePath + "Compac-Trama (" + Puerto + ").txt";
                //SWTramas = File.AppendText(ArchivoTramas);

                ////Escribe encabezado
                //SWTramas.WriteLine();
                //SWTramas.WriteLine("=====================================================================");
                //SWTramas.WriteLine(DateTime.Now);
                //SWTramas.WriteLine("COMPAC");
                //SWTramas.WriteLine("Archivo de Tramas transmitidas y Tramas Recibidas");
                //SWTramas.WriteLine("=====================================================================");
                //SWTramas.Flush();

                //Instancia los eventos de los objetos Timer
                //PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);

                ////Instancia los eventos disparados por la aplicacion cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);

                //Instancia los eventos disparados por la aplicación cliente
               // Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
               //// oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
               // //oEvento.CambioPrecio += new SharedEventsFuelStation.__CMensaje_CambioPrecioEventHandler(oEvento_CambioPrecio);
               // oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
               // oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
               // oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
               // //oEvento.InactivarCaraCambioTarjeta += new SharedEventsFuelStation.__CMensaje_InactivarCaraCambioTarjetaEventHandler(oEvento_InactivarCaraCambioTarjeta);
               // //oEvento.FinalizarCambioTarjeta += new SharedEventsFuelStation.__CMensaje_FinalizarCambioTarjetaEventHandler(oEvento_FinalizarCambioTarjeta);
               // //oEvento.CambiarDensidad += new SharedEventsFuelStation.__CMensaje_CambiarDensidadEventHandler(oEvento_CambiarDensidad);
               // //oEvento.FinalizarVentaPorMonitoreoCHIP += new SharedEventsFuelStation.__CMensaje_FinalizarVentaPorMonitoreoCHIPEventHandler(oEvento_FinalizarVentaPorMonitoreoCHIP);
               // oEvento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEvento_CerrarProtocolo);



                ////Almacena el numero de caras a encuestarse
                //this.NumerodeCaras = Convert.ToByte(NumerodeCaras + CaraInicial - 1);
                //this.CaraInicial = CaraInicial;

                //EstructuraRedSurtidor = PropiedadCara;

                //Almacena el Precio de Venta establecido para la EDS
                //PrecioEDS = Convert.ToDecimal(strPrecioEDS);

                PuertoSurtidores = Puerto;
                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 2400;
                    PuertoCom.DataBits = 7;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
                    PuertoCom.ReadBufferSize = 128;
                    PuertoCom.WriteBufferSize = 128;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception ex)
                    {
                        throw ex; //throw new Exception ("Comunicacion con surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }


                //Armar diccionario
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;


                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica                
                HiloCicloCaras.Start();

                ////Se dimensiona cada uno de los arreglos utilizados
                //EstadoActual = new EstadoCara[this.NumerodeCaras];
                //EstadoAnterior = new EstadoCara[this.NumerodeCaras];
                //IdleActual = new Idle[this.NumerodeCaras];
                //IdleAnterior = new Idle[this.NumerodeCaras];

                //PredeterminarVolumen = new bool[this.NumerodeCaras];
                //PredeterminarValor = new bool[this.NumerodeCaras];
                //CaraInicializada = new bool[this.NumerodeCaras];
                //AutorizarCara = new bool[this.NumerodeCaras];
                //TomarLecturaApertura = new bool[this.NumerodeCaras];
                //TomarLecturaCierre = new bool[this.NumerodeCaras];
                //FalloComunicacionReportado = new bool[this.NumerodeCaras];
                //FalloTomaLecturas = new bool[this.NumerodeCaras];
                //Autorizando = new bool[this.NumerodeCaras];

                //Precio = new decimal[this.NumerodeCaras];
                //LecturaInicialVenta = new decimal[this.NumerodeCaras];
                //LecturaFinalVenta = new decimal[this.NumerodeCaras];
                //ValorPredeterminado = new decimal[this.NumerodeCaras];
                //Volumen = new decimal[this.NumerodeCaras];
                //Importe = new decimal[this.NumerodeCaras];

                //this.Puerto = new string[this.NumerodeCaras];

                //FalloComunicacion = new bool[2];
                ///* [0] Error en Datos
                // * [1] Error en Comunicación: Trama incompleta o no hay respuesta del surtidor*/

                ////Establece inicio de encuesta en Cara Inicial
                //CaraEncuestada = CaraInicial;





                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraInicial + "|Proceso|Cara Inicial");
                SWRegistro.Flush();

                //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
                //PollingTimer.AutoReset = true;                

                //Se activa el timer por primera vez
                //PollingTimer.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Compac";
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + this.NumerodeCaras + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        private void CicloCara()
        {
            try
            {
                //Variable para garantizar el ciclo infinito
                CondicionCiclo = true;

                //Ciclo Infinito
                while (CondicionCiclo)
                {
                    try
                    {
                        VerifySizeFile();
                        //Ciclo de recorrido por las caras
                        foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                        {
                            //Si la cara está activa, realizar proceso de encuesta
                            if (ORedCaras.Activa == true)
                            {
                                CaraEncuestada = ORedCaras.Cara;


                                EncuestarCara();

                                ////Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                //if (ProcesoEnvioComando(ComandoSurtidor.RealTime))
                                //    TomarAccion();
                            }
                        }
                    }
                    catch (Exception Excepcion)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|CondicionCiclo: " + Excepcion);
                        SWRegistro.Flush();
                    }  

                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método CicloCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }




        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE ENVIA COMANDO RESPECTIVO Y SE TOMAN LAS ACCIONES
        private void EncuestarCara()
        {
            try
            {
                int Reintentos = 0;
                //Solamente ingresa a esta parte de código cuando no se ha inicializado la cara (inicio de programa)
                
                //if (CaraInicializada[CaraEncuestada] == false)
                if(EstructuraRedSurtidor[CaraEncuestada].CaraInicializada == false)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicializacion de Cara al arrancar protocolo");
                    SWRegistro.Flush();
                    InicializarCara();
                }

                //Realiza la respectiva tarea en la normal ejecución del proceso
                //switch (EstadoActual[CaraEncuestada])
                switch (EstructuraRedSurtidor[CaraEncuestada].Estado)

                {
                    case EstadoCara.Espera:
                        //Si la cara está en proceso de autorización
                        //if (Autorizando[CaraEncuestada] == false)
                        if (EstructuraRedSurtidor[CaraEncuestada].Autorizando == false)
                        {
                            //Envia comando de Request Status para establecer si hubo cambio de Estado
                            if (!ProcesoEnvioComando(ComandoSurtidor.Estado))
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No respondio comando de Estado en Estado Espera");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                //Si la manguera se encuentra colgada                                
                                //if (IdleActual[CaraEncuestada - 1] == Idle.Colgada)
                                if (EstructuraRedSurtidor[CaraEncuestada].IdleActual == EstadoManguera.Colgada)
                                {
                                    //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                                    if ((EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true) || (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true))
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicia toma de lecturas para Apertura/Cierre de turno");
                                        SWRegistro.Flush();
                                        LecturaAperturaCierre();
                                    }
                                }
                                //Si la manguera se encuentra descolagada
                                else
                                {
                                    //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado Levantada
                                    if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas == false)
                                    {
                                        string MensajeErrorLectura = "Manguera descolgada";
                                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                                        {
                                            //Se establece valor de la variable para que indique que ya fue reportado el error
                                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas = true;
                                            bool EstadoTurno = false;
                                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                            SWRegistro.Flush();
                                        }
                                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                                        {
                                            //Se establece valor de la variable para que indique que ya fue reportado el error
                                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas = true;
                                            bool EstadoTurno = true;
                                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                            SWRegistro.Flush();
                                        }
                                    }

                                    //Revisa en el vector de Autorizacion si la venta se debe autorizar
                                    //if (AutorizarCara[CaraEncuestada] == true)
                                     if(EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                                    {
                                        //Setea variable que indica que la cara esta en proceso de autorización
                                        //Autorizando[CaraEncuestada - 1] = true;
                                        EstructuraRedSurtidor[CaraEncuestada].Autorizando = true;

                                        //Obtiene la Lectura Inicial de la Venta
                                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura Inicial de Venta");
                                        SWRegistro.Flush();
                                        RecuperarTotalizador();

                                        /* Si no obtiene correctamente el valor del Totalizador la Lectura Inicial de la venta la hace 
                                         * igual a la lectura final de la venta anterior*/
                                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura > 0)
                                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura;
                                        else
                                        {
                                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta;
                                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Lectura inicial en 0, asume Lectura Final");
                                            SWRegistro.Flush();
                                        }

                                        string strLecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta.ToString("N3");
                                        LecturaInicialVenta(CaraEncuestada, strLecturaInicialVenta);
                                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa Lectura Inicial de Venta: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta);
                                        SWRegistro.Flush();

                                        //Envía comando de Autorización
                                        //Reintentos = 0;
                                        //do
                                        //{                                        
                                        //ProcesoEnvioComando(ComandoSurtidor.Autorizar);
                                        //    Reintentos++;
                                        //    Thread.Sleep(30);
                                        //} while (EstadoActual[CaraEncuestada - 1] != EstadoCara.Pre_Despacho &&
                                        //    EstadoActual[CaraEncuestada - 1] != EstadoCara.Despachando && Reintentos <= 3);                                                                       

                                        //Envía comando de Autorización
                                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Autorización de Venta 1");
                                        SWRegistro.Flush();
                                        if (ProcesoEnvioComando(ComandoSurtidor.Autorizar))
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Autorizando Venta 1");
                                            SWRegistro.Flush();
                                        }
                                        else
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No respondio a comando Autorizar 1");
                                            SWRegistro.Flush();
                                        }
                                    }
                                }
                            }                            
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Autorización de Venta 2");
                            SWRegistro.Flush();
                            if (ProcesoEnvioComando(ComandoSurtidor.Autorizar))
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Autorizando Venta 1");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No respondio a comando Autorizar 2");
                                SWRegistro.Flush();
                            }
                        }
                        break;

                    case EstadoCara.Despachando :
                    case EstadoCara.FinDespacho:
                    case EstadoCara.Pre_Despacho:
                    case EstadoCara.DespachoEnCero:
                    case EstadoCara.FinalizandoDespachoPredeterminado:
                        
                        //Da valores a las variables de control respectivas
                        EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                        //Autorizando[CaraEncuestada - 1] = false;
                        EstructuraRedSurtidor[CaraEncuestada].Autorizando = false; 
                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                        
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado En Despacho
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas == false)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas = true;
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas = true;
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }
                    
                        //Reset del elemento que indica que la Cara debe ser autorizada
                        //if (AutorizarCara[CaraEncuestada - 1] == true)
                        //    AutorizarCara[CaraEncuestada - 1] = false;                        
                        
                        //Recupera parciales o final de venta
                        RecuperarDatosDespacho();
                        break;
                    
                    case EstadoCara.StartUp:
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Envia comando InicializarCara");
                        SWRegistro.Flush();
                        ProcesoEnvioComando(ComandoSurtidor.InicializarCara);
                        break;

                    default:
                        ProcesoEnvioComando(ComandoSurtidor.Estado);
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método TomarAccion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //SETEA VALORES POR DEFECTO (PRECIO Y POTENCIA DE 10 DIVISORA)
        private void InicializarCara()
        {
            try
            {
                //Factor de división por defecto del Precio
                if (EstructuraRedSurtidor[CaraEncuestada].FactorPrecio == 0)
                    EstructuraRedSurtidor[CaraEncuestada].FactorPrecio = 100;

                //Factor de división por defecto del TotalVolumen
                if (EstructuraRedSurtidor[CaraEncuestada].FactorVolumen == 0)
                    EstructuraRedSurtidor[CaraEncuestada].FactorVolumen = 100;

                //Factor de división por defecto del TotalDinero 
                if (EstructuraRedSurtidor[CaraEncuestada].FactorImporte == 0)
                    EstructuraRedSurtidor[CaraEncuestada].FactorImporte = 100;

                //Factor de división por defecto del Totalizadores
                if (EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador == 0)
                    EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador = 100;

                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Envia comando InicializarCara");
                SWRegistro.Flush();

                //Pide el estado de la cara
                if (ProcesoEnvioComando(ComandoSurtidor.InicializarCara))
                {
                    //Si la cara esta en reposo
                    if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera && EstructuraRedSurtidor[CaraEncuestada].IdleActual == EstadoManguera.Colgada)
                    {
                        //Obtiene el precio, para determinar si éste debe ser cambiado en los surtidores
                        RecuperarPrecio();

                        //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                        if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta != EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 && EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 != 0) //dcf CARGAR EL PRECIO AL INICIALIZAR EL PROTOCOLO ??
                            CambiarPrecio(); 

                        //Cambia bandera inidicando que la cara fue inicializada correctamente
                        EstructuraRedSurtidor[CaraEncuestada].CaraInicializada  = true;

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicializada");
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Estado inválido para cambiar percio: Estado" +
                            EstructuraRedSurtidor[CaraEncuestada].Estado + ", Idle" + EstructuraRedSurtidor[CaraEncuestada].IdleActual);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No respondio comando de Inicializacion de Cara");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método InicializarCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        private bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                //Asigna a la cara a encuestar el comando que fue enviado
                ComandoCaras = ComandoaEnviar;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 3;

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa el vector de control de fallo de comunicación
                //FalloComunicacion[0] = false;
                //FalloComunicacion[1] = false;
                FalloComunicacion = false;

                //Arma la trama de Transmision
                ArmarTramaTx();

                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {
                    EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor
                    if (BytesEsperados > 0)
                    {
                        RecibirInformacion();
                        Reintentos += 1;
                    }
                } //while ((FalloComunicacion[0] == true || FalloComunicacion[1] == true) && (Reintentos < MaximoReintento));
                while ((FalloComunicacion == true) && (Reintentos < MaximoReintento)) ;


                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                //if (FalloComunicacion[0] == true || FalloComunicacion[1] == true)
                if (FalloComunicacion == true)
                {
                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    //if (FalloTomaLecturas[CaraEncuestada] == false)
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas == false)                        
                    {
                        string MensajeErrorLectura = "Error en Comunicación con Surtidor";
                        //if (TomarLecturaApertura[CaraEncuestada] == true)
                        if(EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas = true;
                            bool EstadoTurno = false;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        //if (TomarLecturaCierre[CaraEncuestada] == true)
                        if(EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturas = true;
                            bool EstadoTurno = true;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                    }

                    //if (FalloComunicacion && !FalloComunicacionReportado[CaraEncuestada])
                    if(FalloComunicacion && !EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Perdida de comunicacion en comando " + ComandoCaras);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = true;
                        //oEvento.ReportarErrorComunicacion( CaraEncuestada);                    
                    }
                    if (!FalloComunicacion && EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Se reestablece comunciación con surtidor en comando " + ComandoCaras);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro FALSE si hubo error en la trama o en la comunicación con el surtidor
                    return false;
                }
                else
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Se reestablece comunciación con surtidor en comando " + ComandoCaras);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ProcesoEnvioComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        private void ArmarTramaTx()
        {
            try
            {
                char chrCaraEncuestada  = new char();
                //if (EstructuraRedSurtidor[CaraEncuestada].Cara < 10)
                if(CaraEncuestada < 10)
                    //chrCaraEncuestada = Convert.ToChar(CaraEncuestada.ToString());
                    chrCaraEncuestada = Convert.ToChar(CaraEncuestada.ToString());
                else
                {
                    switch (CaraEncuestada)
                    //switch(EstructuraRedSurtidor[CaraEncuestada].Cara)
                    {
                        case 10:
                            chrCaraEncuestada = ':';
                            break;
                        case 11:
                            chrCaraEncuestada = ';';
                            break;
                        case 12:
                            chrCaraEncuestada = '<';
                            break;
                        case 13:
                            chrCaraEncuestada = '=';
                            break;
                        case 14:
                            chrCaraEncuestada = '>';
                            break;
                        case 15:
                            chrCaraEncuestada = '?';
                            break;
                        case 16:
                            chrCaraEncuestada = '@';
                            break;                        
                    }
                }

                //Dependiendo del Comando a Enviar se Construye la Trama
                switch (ComandoCaras)
                {
                    case ComandoSurtidor.Autorizar:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       1-Authorise
                         * 6 -> Poll type:  Q-Request status (no data)
                         * 7 -> Checksum:   Cálculo
                         * 8 -> CR:         Carriage Return
                         * 9 -> LF:         Line Feed
                         */
                        //AUTORIZA BIENTramaTx = new char[12] { '*', 'T', chrCaraEncuestada, 'A', '1', 'A', 'H', '1', '%', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '1', 'Q', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 200;
                        BytesEsperados = 10;
                        break;      
              
                    case ComandoSurtidor.Reautorizar:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       5-Clear temporary stop
                         * 6 -> Poll type:  Q-Request status (no data)
                         * 7 -> Checksum:   Cálculo
                         * 8 -> CR:         Carriage Return
                         * 9 -> LF:         Line Feed
                         */
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '5', 'Q', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 200;
                        BytesEsperados = 10;
                        break;      

                    case ComandoSurtidor.Estado:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       3-Hold, 4-Temporary Stop
                         * 6 -> Poll type:  Q-Reques status (no data)
                         * 7 -> Checksum:   Cálculo
                         * 8 -> CR:         Carriage Return
                         * 9 -> LF:         Line Feed
                         */
                        switch (EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {                            
                            case EstadoCara.Espera:
                                TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '3', 'Q', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                                break;
                            default:                                
                                TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '4', 'Q', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                                break;
                        }
                        TimeOut = 200;
                        BytesEsperados = 10;
                        break;                   

                    case ComandoSurtidor.ObtenerPrecio:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       3-Hold
                         * 6 -> Poll type:  C-Request price (Single Host)
                         * 7 -> Checksum:   Cálculo
                         * 8 -> CR:         Carriage Return
                         * 9 -> LF:         Line Feed
                         */
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '3', 'C', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 200;
                        BytesEsperados = 15;
                        break;

                    case ComandoSurtidor.ObtenerDatosDespacho:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       1-Authorise
                         * 6 -> Poll type:  D-Request delivery amount and quantity
                         * 7 -> Checksum:   Cálculo
                         * 8 -> CR:         Carriage Return
                         * 9 -> LF:         Line Feed
                         */
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '1', 'D', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 300;
                        BytesEsperados = 24;
                        break;

                    case ComandoSurtidor.TerminarVenta:
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '6', 'Q', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 200;
                        BytesEsperados = 10;
                        break;

                    case ComandoSurtidor.ObtenerTotalizadores:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       1-Authorise
                         * 6 -> Poll type:  a-Request electronic totals
                         * 7 -> FID:        H-Hose number
                         * 8 -> Data:       Numero de Hose
                         * 9 -> EOF:        %
                         * 10-> Checksum:   Cálculo
                         * 11-> CR:         Carriage Return
                         * 12-> LF:         Line Feed
                         */
                        TramaTx = new char[12] { '*', 'T', chrCaraEncuestada, 'A', '3', 'a', 'H', '1', '%', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 350;
                        BytesEsperados = 37;
                        break;

                    case ComandoSurtidor.InicializarCara:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       9-Clear start-up/initialise (Status 15)
                         * 6 -> Poll type:  a-Request electronic totals
                         * 7 -> FID:        H-Hose number
                         * 8 -> Data:       Numero de Hose
                         * 9 -> EOF:        %
                         * 10-> Checksum:   Cálculo
                         * 11-> CR:         Carriage Return
                         * 12-> LF:         Line Feed
                         */
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '9', 'Q', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 200;
                        BytesEsperados = 10;
                        break;

                    case ComandoSurtidor.EnviarPrecio:
                        /*
                         * 4 -> Mode:       A-Postpay
                         * 5 -> Lock:       7-Allow price change (immediate)
                         * 6 -> Poll type:  P-Send price (MPD)
                         * -------------------------------------------------
                         * 7 -> FID:        G-New price
                         * 8 -> Data:       1
                         *                  4
                         *                  3
                         * 11-> EOF:        %
                         * -------------------------------------------------
                         * 12-> Checksum:   Cálculo
                         * 13-> CR:         Carriage Return
                         * 14-> LF:         Line Feed
                         */

                        TramaTx = new char[14] { '*', 'T', chrCaraEncuestada, 'A', '8', 'G', 'G', '0', '0', '0', '0', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Estableciendo Nuevo Precio: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 );
                        SWRegistro.Flush();

                        //Almacena en una cadena el nuevo precio, luego de convertirlo en un valor String Entero

                    //string strNuevoPrecio = Convert.ToInt32(PrecioEDS * EstructuraRedSurtidor[CaraEncuestada].FactorPrecio).ToString().PadLeft(4,'0');
                        string strNuevoPrecio = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 * EstructuraRedSurtidor[CaraEncuestada].FactorPrecio).ToString().PadLeft(4, '0');

                     
                        if (strNuevoPrecio.Substring(0, 1) == "0")
                            TramaTx[10] = '%';
                        //Si la cifra de precio lleva 4 números, el dígito más significativo lo posiciona en el campo 10 (EOF = %)
                        else
                            TramaTx[10] = Convert.ToChar(strNuevoPrecio.Substring(0, 1));

                        for (int i = 1; i < 4; i++)
                            TramaTx[i + 6] = Convert.ToChar(strNuevoPrecio.Substring(i + strNuevoPrecio.Length - 4, 1));
                                                
                        TimeOut = 300;
                        BytesEsperados = 15;
                        break;

                    case ComandoSurtidor.ObtenerPosicionDecimal:
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '3', 'o', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 300;
                        BytesEsperados = 28;
                        break;
                    case ComandoSurtidor.ObtenerVersionFirmware:
                        TramaTx = new char[9] { '*', 'T', chrCaraEncuestada, 'A', '3', 'j', ' ', Convert.ToChar(0x0D), Convert.ToChar(0x0A) };
                        TimeOut = 300;
                        BytesEsperados = 21;
                        break;
                }

                //Escribe mensaje de tramas en el archivo
                SWTramas.Flush();

                //Calcula la longitud del mensaje y el byte de Redundancia Ciclica (CRC)
                TramaTx[TramaTx.Length - 3] = ObtenerCRC(TramaTx);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ArmarTramaTx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
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

                //Escribe la trama enviada en el archivo de texto
                string strTramaTx = new string(TramaTx).Substring(0, TramaTx.Length - 2);
                SWTramas.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Tx|" + ComandoCaras + "|" + strTramaTx);
                SWTramas.Flush();
                
                //Tiempo muerto mientras el Surtidor Responde
                Thread.Sleep(TimeOut);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método EnviarComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //LEE Y ALMACENA LA TRAMA RECIBIDA
        private void RecibirInformacion()
        {
            try
            {                              
                //Almacena informacion en una Trama Temporal para luego eliminarle el eco
                int Bytes = PuertoCom.BytesToRead;

                char[] TramaTemporal = new char[Bytes];
                PuertoCom.Read(TramaTemporal, 0, Bytes);
                PuertoCom.DiscardInBuffer();

                int BytesRespuesta = 0;

                if (Bytes > 0)
                    BytesRespuesta = Bytes - (TramaTx.Length + 1);

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                if (BytesRespuesta == BytesEsperados)
                {
                    //Se dimensiona la Trama a evaluarse (TramaRx)
                    TramaRx = new char[TramaTemporal.Length - TramaTx.Length - 1];

                   

                    //Almacena los datos reales (sin eco) en TramaRx
                    for (int i = 0; i <= (TramaTemporal.Length - TramaTx.Length - 2); i++)
                        TramaRx[i] = TramaTemporal[i + TramaTx.Length + 1];


                    //borra solo para pruerbas DCF
                    ////
                    //TramaRx = new Char[] { '*', 'F', '1', 'v', '0', 'A', '9', '^' };//*F1v0A9^
                    //TramaRx = new Char[] { '*', 'F', '1', 'v', '0', 'A', '3', 'C', '2', '9', '0', '1', 'U', }; // *F1v0A3C2901U
                    //TramaRx = new Char[] { '*', 'F', '1', 'v', '0', 'A', '3', 'd' };//*F1v0A3d
                    //TramaRx = new Char[] { '*', 'F', '1', 't', '0', 'A', '3', 'f' };//*F1t0A3f --porautoriza autorizar *F1t0A3f
                    //TramaRx = new char[] { '*', 'F', '1', 't', '0', 'A', '3', 'H', '1', '%', 'M', '0', '0', '2', '7', '4', '0', '3', '2', '0', '8', '%', 'Q', '0', '0', '2', '3', '8', '4', '5', '8', '8', '8', '%', 'X' };

                    //TramaRx = new char[] { '*', 'F', '1', 't', '0', 'A', '1', 'h' };
                    //TramaRx = new char[] { '*', 'F', '1', 's', '0', 'A', '1', 'i' };//*F1s0A1i
                    //TramaRx = new char[] { '*', 'F', '1', 'w', '2', 'A', '1', 'c' };//*F1w2A1c
                    //TramaRx = new char[] { '*', 'F', '1', 'w', '2', 'A', '1', 'D', '0', '0', '0', '0', '0', '%', 'L', '0', '0', '0', '0', '0', '%', 'i' };//inicio despacho 
                    //TramaRx = new char[] {'*','F','1','w','3','A','1','D','0','0','9','8','3','%','L','0','0','7','6','2','%','E'}; //en despacho 
                    //TramaRx = new char[] {'*','F','1','u',':','A','1','D','0','1','0','2','2','%','L','0','0','7','9','2','%','L'};//fin de venta                     
                    //TramaRx = new char[] {'*','F','1','u','0','A','6','b'};
                    //TramaRx = new char[] {'*','F','1','u','0','A','3','H','1','%','M','0','0','2','7','4','0','4','2','3','0','%','Q','0','0','2','3','8','4','6','6','8','0','%','d'};

                    //TramaRx = new char[] {'*','F','1','u','0','A','3','C','2','9','0','1','V'};

                    //TramaRx = new char[] {'*','F','1','u','0','A','3','e'};


                    //Escribe la trama recibida en el archivo de texto
                    string strTramaRx2 = new string(TramaRx);
                    SWTramas.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Rx|" + ComandoCaras + "|" + strTramaRx2);
                    SWTramas.Flush();

                    AsignarEstado();
                }
                else 
                {
                    FalloComunicacion = true;

                    /////////////////////////////////////////////////////////////////////////////////////////////////
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Comando " + ComandoCaras + ", " + BytesEsperados + " bytes esperados - " + 
                        BytesRespuesta + " bytes de respuesta recibidos");
                    SWRegistro.Flush();
                    /////////////////////////////////////////////////////////////////////////////////////////////////

                    //Escribe la trama recibida en el archivo de texto
                    string strTramaRx2 = new string(TramaTemporal);
                    SWTramas.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Rx|" + ComandoCaras + "|" + strTramaRx2);
                    SWTramas.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA LA TRAMA: CAMPOS, CORRESPONDENCIA DE CARA ENCUESTADA Y CARA QUE RESPONDE, CRC, ETC
        //private void AnalizarTramaRecibida()
        //{
        //    try
        //    {
        //        /* En este método se analizará y evaluará cada uno de los campos de la trama
        //         * Si todo marcha bien, debe AsignarEstado de una
        //         */

        //        AsignarEstado();

        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepción en el Método AnalizarTrama: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + " " + MensajeExcepcion);
        //        SWRegistro.Flush();
        //    }
        //}   

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void AsignarEstado()
        {
            try
            {
                //Almacena último estado de venta de la manguera
                //if (EstadoAnterior[CaraEncuestada] != EstadoActual[CaraEncuestada])
                if(EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                {
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado);
                    SWRegistro.Flush();
                }

                //Almacena último estado físico de la manguera
                //if (IdleAnterior[CaraEncuestada] != IdleActual[CaraEncuestada])   
                if(EstructuraRedSurtidor[CaraEncuestada].IdleAnterior != EstructuraRedSurtidor[CaraEncuestada].IdleActual)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Idle|" + EstructuraRedSurtidor[CaraEncuestada].IdleActual);
                    SWRegistro.Flush();
                    EstructuraRedSurtidor[CaraEncuestada].IdleAnterior = EstructuraRedSurtidor[CaraEncuestada].IdleActual;
                }

                int TempIdle = (TramaRx[3] - 0x3B);
                TempIdle = TempIdle & 0x02;
                //Asigna estado físico de la cara
                switch (TempIdle)
                {
                    //case 0:
                    //    IdleActual[CaraEncuestada - 1] = Idle.Extraida;
                    //    break;
                    //case 2:
                    //    IdleActual[CaraEncuestada - 1] = Idle.Colgada;
                    //    break;

                    case 0:
                        EstructuraRedSurtidor[CaraEncuestada].IdleActual = EstadoManguera.Extraida;
                        break;
                    case 2:
                        EstructuraRedSurtidor[CaraEncuestada].IdleActual = EstadoManguera.Colgada;
                        break;
                }


                //SWRegistro.WriteLine(DateTime.Now + " Cara " + CaraEncuestada + ": " + EstadoAnterior[CaraEncuestada - 1] + ", " + EstadoActual[CaraEncuestada - 1]);
                //SWRegistro.WriteLine(DateTime.Now + " Cara " + CaraEncuestada + ": " + IdleAnterior[CaraEncuestada - 1] + ", " + IdleActual[CaraEncuestada - 1]);
                //SWRegistro.Flush();

                //Asigna estado de la cara
                switch (TramaRx[4])
                {
                    case '0':
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Espera;

                        //Informa cambio de estado
                        if (EstructuraRedSurtidor[CaraEncuestada].IdleActual == EstadoManguera.Colgada)
                        {
                            //Si hay cambio de Estado Interno a Espera o Físico a Colgada , informa cambio de estado
                            if (EstructuraRedSurtidor[CaraEncuestada].IdleActual != EstructuraRedSurtidor[CaraEncuestada].IdleAnterior ||
                                EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa cara en Espera");
                                SWRegistro.Flush();
                                //oEvento.InformarCaraEnReposo( CaraEncuestada);
                                int mangueraColgada = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD;
                               CaraEnReposo( CaraEncuestada,  mangueraColgada);
                            }
                        }
                        else
                        {
                            //Si hay cambio de Estado Interno a Espera o Físico a Colgada , informa cambio de estado
                            if (EstructuraRedSurtidor[CaraEncuestada].IdleActual != EstructuraRedSurtidor[CaraEncuestada].IdleAnterior ||
                                EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion");
                                SWRegistro.Flush();
                                //oEvento.RequerirAutorizacion( CaraEncuestada);
                               
                                int IdProducto = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].IdProducto;
                                int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD;
                                string Lectura = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");
                               AutorizacionRequerida( CaraEncuestada,  IdProducto,  IdManguera,  Lectura,"");

                            }
                        }

                        //if (IdleAnterior[CaraEncuestada - 1] != IdleActual[CaraEncuestada - 1])
                        //{
                        //    if (IdleActual[CaraEncuestada - 1] == Idle.Colgada)
                        //       CaraEnReposo( CaraEncuestada);
                        //    else
                        //       AutorizacionRequerida( CaraEncuestada);
                        //}
                        //else if (EstadoActual[CaraEncuestada - 1] != EstadoAnterior[CaraEncuestada - 1]
                        //   CaraEnReposo( CaraEncuestada);

                        break;
                    case '1':
                        //EstadoActual[CaraEncuestada] = EstadoCara.Autorizada;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Autorizada;
                        break;
                    case '2':
                        //EstadoActual[CaraEncuestada] = EstadoCara.Pre_Despacho;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Pre_Despacho;
                        break;
                    case '3':
                        //EstadoActual[CaraEncuestada] = EstadoCara.Despachando;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despachando;
                        break;
                    case '4':
                        //EstadoActual[CaraEncuestada] = EstadoCara.FlujoLento;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FlujoLento;
                        break;
                    case '5':
                        //EstadoActual[CaraEncuestada] = EstadoCara.FinalizandoDespachoPredeterminado;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinalizandoDespachoPredeterminado;
                        break;
                    case '6':
                        //EstadoActual[CaraEncuestada] = EstadoCara.FinalizandoDespachoNozzle;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinalizandoDespachoNozzle;
                        break;
                    case '7':
                        //EstadoActual[CaraEncuestada] = EstadoCara.DespachoEnCero;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.DespachoEnCero;
                        break;
                    case '8':
                        //EstadoActual[CaraEncuestada] = EstadoCara.FinDespachoPredeterminado;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinalizandoDespachoPredeterminado;
                        break;
                    case '9':
                        //EstadoActual[CaraEncuestada] = EstadoCara.CambioPrecioEnProceso;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.CambioPrecioEnProceso;
                        break;                    
                    case ':':
                        //EstadoActual[CaraEncuestada] = EstadoCara.FinDespacho;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespacho;
                        break;
                    case '?':
                        //EstadoActual[CaraEncuestada] = EstadoCara.StartUp;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.StartUp;
                        break;
                    case 'e':
                        //EstadoActual[CaraEncuestada] = EstadoCara.Error;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.ErrorCompac;
                        break;
                    case '>':
                        //EstadoActual[CaraEncuestada] = EstadoCara.CambiadoFactorK;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.CambiadoFactorK;
                        break;
                    case '=':
                        //EstadoActual[CaraEncuestada] = EstadoCara.IdentificadorDisponible;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.IdentificadorDisponible;
                        break;
                    case '!':
                        //EstadoActual[CaraEncuestada] = EstadoCara.BadCard;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.BadCard;
                        break;
                    case 'd':
                        //EstadoActual[CaraEncuestada] = EstadoCara.PinpadAvailable;
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PinpadAvailable;
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método AsignarEstado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        
        //RECUPERA LOS DATOS DE PARCIALES O FINALES DE VENTA
        private void RecuperarDatosDespacho()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                string strImporte;
                string strVolumen;
                EstructuraRedSurtidor[CaraEncuestada].Volumen = 0;
                //Importe[CaraEncuestada] = 0;
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = 0; 

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {                    
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerDatosDespacho))
                    {
                        //Inicializacion de variables con los valores recibidos en la trama
                        strImporte = new string(TramaRx, 8, 5);
                        strVolumen = new string(TramaRx, 15, 5);

                        //SWRegistro.WriteLine(DateTime.Now + " Cara " + CaraEncuestada + ": Datos Despacho: " + strVolumen +
                        //    "m3 - S/." + strImporte);
                        //SWRegistro.Flush();


                        //Si el EOF  de IMPORTE es diferente de %, quiere decir que viene el dígito más significativo
                        if (TramaRx[13] != '%')
                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta = Convert.ToDecimal(strImporte) + Convert.ToDecimal(TramaRx[13].ToString()) * 100000;
                        else
                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta = Convert.ToDecimal(strImporte);

                        //Si el EOF de VOLUMEN es diferente de %, quiere decir que viene el dígito más significativo, en este caso los millones (sin dividir)
                        if (TramaRx[20] != '%')
                            EstructuraRedSurtidor[CaraEncuestada].Volumen = Convert.ToDecimal(strVolumen) + Convert.ToDecimal(TramaRx[20].ToString()) * 100000;
                        else
                            EstructuraRedSurtidor[CaraEncuestada].Volumen = Convert.ToDecimal(strVolumen);


                        //Almacena valores en el Arreglo respectivo
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;
                        EstructuraRedSurtidor[CaraEncuestada].Volumen = EstructuraRedSurtidor[CaraEncuestada].Volumen / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;

                        if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despachando ||
                            EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinalizandoDespachoNozzle ||
                            EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinalizandoDespachoPredeterminado ||
                            EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Pre_Despacho)
                        {
                            //Reporta Parciales de Venta
                            strImporte = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                            strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                            VentaParcial( CaraEncuestada,  strImporte,  strVolumen);

                            //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                            if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                                EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                        }
                        //Reporta FINALES DE VENTA
                        else if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespacho || 
                            EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoPredeterminado ||
                            EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera && 
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                        {
                            //Reset del elemento que indica que la Cara debe ser autorizada
                            if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                                EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Recuperando Datos Adicioanles para fin de venta");
                            SWRegistro.Flush();
                            RecuperarDatosAdicionalesFindeVenta();

                            if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0)
                            {
                                //Dispara evento al programa principal si la venta es diferente de 0
                                strImporte = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N2");
                                strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N2");
                                string strPrecio = EstructuraRedSurtidor[CaraEncuestada].PrecioVenta.ToString("N2");
                                string strLecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta.ToString("N3");
                                string bytProducto =  EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].IdProducto.ToString("N3");//DCF 10/04/2018
                                int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD;
                                string strLecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta.ToString("N3");

                                //Variable para soportar SharedEvents
                                string Presion = "0";
                                //Informa finalización de venta
                                //oEvento.InformarFinalizacionVenta( CaraEncuestada,  strImporte,  strPrecio,  strLecturaFinalVenta,  strVolumen,  bytProducto);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa fin de venta: Importe: " + strImporte +
                                    " - Precio: " + strPrecio + " - Lectura Final: " + strLecturaFinalVenta + " - Volumen: " + strVolumen + " - Presión: " + Presion + " - Producto " + bytProducto);
                                SWRegistro.Flush();
                                string Densidad = "0";
                                //oEvento.InformarFinalizacionVenta( CaraEncuestada,  strImporte,  strPrecio,  strLecturaFinalVenta,  strVolumen,  bytProducto,  Densidad);

                               VentaFinalizada( CaraEncuestada,  strImporte,  strPrecio,  strLecturaFinalVenta,  strVolumen, bytProducto,  IdManguera,  Presion,  strLecturaInicialVenta);



                                //Se borra la lectura inicial para que no sea utilizada en la próxima venta. DCF 0206-2012
                                //LecturaInicialVenta[CaraEncuestada] = 0;
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = 0;

                                //Informa cara en reposo
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa cara en Espera");
                                SWRegistro.Flush();
                                //oEvento.InformarCaraEnReposo( CaraEncuestada);
                                int mangueraColgada = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD;
                               CaraEnReposo( CaraEncuestada,  mangueraColgada);

                            
                            }
                            else
                            {
                                //Se borra la lectura inicial para que no sea utilizada en la próxima venta. DCF 0206-2012
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = 0;

                                VentaInterrumpidaEnCero( CaraEncuestada);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Reporta venta en CERO");
                                SWRegistro.Flush();
                            }

                            //Resetea bandera que indica que hay una venta en curso
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                        }
                        else if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.DespachoEnCero && EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                        {
                            //Se borra la lectura inicial para que no sea utilizada en la próxima venta. DCF 0206-2012
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = 0;

                            VentaInterrumpidaEnCero( CaraEncuestada);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Reporta venta en CERO. Estado VentaCero");
                            SWRegistro.Flush();
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;                           
                        }  

                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No respondio a ObtenerDatosDespacho");
                        SWRegistro.Flush();
                    }
                    Reintentos += 1;
                } while ((EstructuraRedSurtidor[CaraEncuestada].Volumen == 0) && Reintentos < 2);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecuperarDatosDespacho: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        
        //REALIZA PROCESO DE FIN DE VENTA
        private void RecuperarDatosAdicionalesFindeVenta()
        {
            try
            {
                //Inicializa nuevamente la variable de Reintentos
                int Reintentos = 0;
                //Desbloquea cara
                do
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Enviando Comando TerminarVenta");
                    SWRegistro.Flush();

                    //Envia comando para Borrar Estado de Fin de Venta
                    ProcesoEnvioComando(ComandoSurtidor.TerminarVenta);
                    Reintentos++;
                } while (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespacho && Reintentos <= 3);

                //Evalúa nuevamente estado, para terminar proceso
                if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera)
                {

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Recuperando Totalizadores para reportar Fin de Venta");
                    SWRegistro.Flush();

                    //Recupera la lectura final
                    RecuperarTotalizador();
                    //LecturaFinalVenta[CaraEncuestada] = Lectura;
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura; 

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Recuperando Precio para reportar Fin de Venta");
                    SWRegistro.Flush();
                    //Recupera precio
                    RecuperarPrecio();

                    //Realiza procedimiento de Control de VOLUMEN DE VENTA
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta > 0)
                    {
                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta >= EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta)
                        {
                            //Calcula el Volumen a partir de la lectura inicial y final, para luego compararlo con el obtenido en Datos Despacho
                            decimal VolumenCalculado = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta - EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta;

                            //if (Volumen[CaraEncuestada] < VolumenCalculado - Convert.ToDecimal(0.5) || Volumen[CaraEncuestada] > VolumenCalculado + Convert.ToDecimal(0.5))
                            if (EstructuraRedSurtidor[CaraEncuestada].Volumen < VolumenCalculado - Convert.ToDecimal(0.5) || EstructuraRedSurtidor[CaraEncuestada].Volumen > VolumenCalculado + Convert.ToDecimal(0.5))
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Volumen Calculado: " +
                                    VolumenCalculado + " - Volumen Reportado: " + EstructuraRedSurtidor[CaraEncuestada].Volumen);
                                SWRegistro.Flush();
                                EstructuraRedSurtidor[CaraEncuestada].Volumen = VolumenCalculado;
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Lectura Inicial: " +
                               EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta + " - Lectura Final: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta);
                            SWRegistro.Flush();

                            //Realiza reajuste de Lectura Final de venta
                            if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Ajusta Lectura Final. Valor: " +
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta + " - Valor Ajustado: " +
                                    (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta + EstructuraRedSurtidor[CaraEncuestada].Volumen));
                                SWRegistro.Flush();
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta + EstructuraRedSurtidor[CaraEncuestada].Volumen;
                            }
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Lectura Inicial en 0");
                        SWRegistro.Flush();
                    }

                    //Realiza procedimiento de Control de IMPORTE DE VENTA
                    if (EstructuraRedSurtidor[CaraEncuestada].TotalVenta != 0)
                    {
                        //decimal ImporteCalculado = EstructuraRedSurtidor[CaraEncuestada].Volumen *EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1;
                        decimal ImporteCalculado = EstructuraRedSurtidor[CaraEncuestada].Volumen * EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                        if (EstructuraRedSurtidor[CaraEncuestada].TotalVenta < ImporteCalculado - Convert.ToDecimal(0.5) || EstructuraRedSurtidor[CaraEncuestada].TotalVenta > ImporteCalculado + Convert.ToDecimal(0.5))
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Importe Calculado: " + 
                                ImporteCalculado + " - Importe Reportado: " + EstructuraRedSurtidor[CaraEncuestada].TotalVenta);
                            SWRegistro.Flush();
                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta = ImporteCalculado;
                        }
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No acepto Comando TerminarVenta");
                    SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecuperarDatosAdicioanlesFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //RECUPERA VALOR DE LA LECTURA
        private void RecuperarTotalizador()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                //Lectura = 0;
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura = 0; 

                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicia Proceso de Obtener Lectura");
                SWRegistro.Flush();
                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizadores))
                    {
                        //Inicializacion de variables con los valores recibidos en la trama
                        string strLectura = new string(TramaRx, 23, 10);

                        //Si el EOF  de Lectura es diferente de %, quiere decir que viene el dígito más significativo
                        if (TramaRx[33] != '%')
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura = Convert.ToDecimal(strLectura) + Convert.ToDecimal(TramaRx[33].ToString()) * 10000000000;
                        else
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura = Convert.ToDecimal(strLectura);

                        //Almacena valores en el Arreglo respectivo
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura / EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador;

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Lectura obtenida " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura);
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No respondio a Comando ObtenerTotalizdores");
                        SWRegistro.Flush();

                        //if (Autorizando[CaraEncuestada])//DCF 14-05-2012 se iguala la lectura final de la venta anterior a la lectura inicial de la venta en curso, en caso de fallo en la toma de lectura inicial.
                        if (EstructuraRedSurtidor[CaraEncuestada].Autorizando)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Se asume Lectura Inicial = Lectura final de la venta anterior: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta);
                            SWRegistro.Flush();
                        }
                    }
                    Reintentos += 1;
                } while ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura == 0) && Reintentos <= 3);


                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Finaliza Obtención de Lectura"); //DCF 01.06.2012
                SWRegistro.Flush();

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecuperarTotalizador: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //RECUPERA EL VALOR DE PRECIO ENVIADO POR EL SURTIDOR
        private void RecuperarPrecio()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                //Precio[CaraEncuestada] = 0;
               // EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 = 0; 


                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicia proceso para obtener Precio");
                SWRegistro.Flush();

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                    {
                        //Inicializacion de variables con los valores recibidos en la trama
                        string strPrecio = new string(TramaRx, 8, 3);                        

                        //Si el EOF es diferente de %, quiere decir que viene el dígito más significativo, en este caso los millones (sin dividir)
                        if (TramaRx[11] != '%')
                            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = Convert.ToDecimal(strPrecio) + Convert.ToDecimal(TramaRx[11].ToString()) * 1000;
                        else
                            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = Convert.ToDecimal(strPrecio);

                        //Le da el formato con los decimales respectivos
                       //EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 =EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;
                        EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = EstructuraRedSurtidor[CaraEncuestada].PrecioVenta / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Precio Obtenido " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                        SWRegistro.Flush();
                    }
                    Reintentos += 1;
                } while ((EstructuraRedSurtidor[CaraEncuestada].PrecioVenta == 0) && Reintentos <= 3);

                //Si el precio no se logró obtener satisfactoriamente, se toma el de base de datos
                if (Convert.ToDecimal(EstructuraRedSurtidor[CaraEncuestada].PrecioVenta) == 0)
                    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecuperarPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
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


                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();
                RecuperarTotalizador();

                ArrayLecturas.Add(Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD) + "|" +
                        Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura));
                LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                ArrayLecturas.CopyTo(LecturasEnvio);

                //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                {
                    string strLecturasVolumen = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa Lectura Final Turno: " + strLecturasVolumen);
                    SWRegistro.Flush(); 
                    //LecturaTurnoCerrado( CaraEncuestada,  strLecturasVolumen);
                    LecturaTurnoCerrado( LecturasEnvio);
                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                }
                //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    string strLecturasVolumen = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa Lectura Inicial Turno: " + strLecturasVolumen);
                    SWRegistro.Flush(); 
                    //oEvento.InformarLecturaInicialTurno( CaraEncuestada,  strLecturasVolumen);
                    LecturaTurnoAbierto(LecturasEnvio);
                    
                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;


                    //Obtiene el precio, para determinar si éste debe ser cambiado en los surtidores
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Toma el precio del surtidor para Iniciar proceso de cambio de precio");
                    SWRegistro.Flush(); 
                    RecuperarPrecio();

                    //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                    if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta != EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inicia proceso Cambio de Precio");
                        SWRegistro.Flush(); 
                        CambiarPrecio();
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
                
        //CAMBIA EL PRECIO DE LA CARA
        private void CambiarPrecio()
        {
            try
            {
                int Reintentos = 0;                
                do
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.EnviarPrecio))
                    {
                        //Inicializacion de variables con los valores recibidos en la trama
                        string strPrecio = new string(TramaRx, 8, 3);

                        //Si el EOF es diferente de %, quiere decir que viene el dígito más significativo, en este caso los millones (sin dividir)
                        if (TramaRx[11] != '%')
                           EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 = Convert.ToDecimal(strPrecio) + Convert.ToDecimal(TramaRx[11].ToString()) * 1000;
                        else
                           EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 = Convert.ToDecimal(strPrecio);

                        //Le da el formato con los decimales respectivos
                       EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 =EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Precio del surtidor: " +EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1);
                        SWRegistro.Flush(); 

                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No aceptó comando de Cambio de Precio");
                        SWRegistro.Flush();
                    }

                    Reintentos += 1;
                } while ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 != EstructuraRedSurtidor[CaraEncuestada].PrecioVenta) && (Reintentos <= 3));

                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 != EstructuraRedSurtidor[CaraEncuestada].PrecioVenta)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|No se pudo establecer nuevo precio");
                    SWRegistro.Flush();
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Precio establecido exitosamente");
                    SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método CambiarPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();

            }
        }
        
        #endregion

        #region METODOS AUXILIARES

        //CALCULA EL CARACTER DE REDUNDANCIA CICLIA
        private char ObtenerCRC(char[] Trama)
        {
            try
            {
                int CRC = new int();
                int j;

                ////////////////////////////////////////////////
                //Trama = new char[9] { '*', 'T', '1', 'A', '1', 'v', ' ', ' ', ' ' };
                ////////////////////////////////////////////////
                //Suma los componentes de la trama, excluyendo el encabezado, la dirección, el Checksum, el CR y el LF
                for (j = 2; j <= Trama.Length - 4; j++)
                    CRC += Convert.ToInt16(Trama[j]);

                //Realizar complemento a 1
                CRC = CRC ^ 0xFF;

                //And con 0x3F y se le suma 0x30
                CRC = CRC & 0x3F;
                CRC += 0x30;
                return Convert.ToChar(CRC);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ObtenerLRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 'e';
            }
        }

        //INICIALIZA VALORES DE LA MATRIZ PARA TOMA DE LECTURAS
        private void IniciaTomaLecturasTurno(string Surtidores, bool Apertura)
        {
            try
            {
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;
                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        //CaraLectura = Convert.ToByte(bSurtidores[i]) * 2 - 1;
                        CaraLectura = Convert.ToByte((Convert.ToInt16(bSurtidores[i]) * 2) - 1);

                        //Si la cara esta en la red  
                        //if (CaraInicial <= CaraLectura && CaraLectura <= NumerodeCaras)
                        if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            //FalloTomaLecturas[CaraLectura] = false;
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturas = false; 

                            if (Apertura)
                            {
                                //TomarLecturaApertura[CaraLectura] = true;    //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaAperturaTurno = true;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|TomarLecturaAperturaTurno = true");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                //TomarLecturaCierre[CaraLectura] = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaCierreTurno = true;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|TomarLecturaCierreTurno = true");
                                SWRegistro.Flush();


                            }

                            //Organiza banderas de pedido de lecturas para la cara PAR
                            //CaraLectura = Convert.ToByte(bSurtidores[i]) * 2;
                            //CaraLectura = Convert.ToByte((Convert.ToInt16(bSurtidores[i]) * 2) - 1);
                            CaraLectura = Convert.ToByte((Convert.ToInt16(bSurtidores[i]) * 2)); //DCF 11/04/2018

                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturas = false;

                            if (Apertura)
                            {
                                //TomarLecturaApertura[CaraLectura] = true;     //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaAperturaTurno = true;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|TomarLecturaAperturaTurno = true");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                //TomarLecturaCierre[CaraLectura] = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaCierreTurno = true;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|TomarLecturaCierreTurno = true");
                                SWRegistro.Flush();

                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|fuera de red de surtidores. Metodo: IniciaTomaLecturasTurno");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método IniciaTomaLecturasTurno: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();

            }
        }

        public void VerifySizeFile() //Logueo
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Compac-Trama (" + COM_Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);

                }



                FileInf = new FileInfo(ArchivoRegistroSucesos);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();                   
                     //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                    ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Compac-Registro (" + COM_Puerto + ").txt";
                    SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                }
            }
            catch (Exception Excepcion)
            {

                string MensajeExcepcion = "Excepción en el Método Logueo: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }



        #endregion

        #region EVENTOS DE LA CLASE
        //SE EJECUTA CADA PERIODO DE TIEMPO
        //private void PollingTimerEvent(object source, ElapsedEventArgs e)
        //{
        //    try
        //    {               

        //        //Se detiene el timer para realizar el respectivo proceso de encuesta
        //        PollingTimer.Stop();

        //        VerifySizeFile(); //DCF control del tamaño de los archivos de logueo Sucesos y Tramas.

        //        //Evalua la cara a encuestar. Si ya termino el recorrido, repite el ciclo
        //        if (CaraEncuestada >= NumerodeCaras)
        //            CaraEncuestada = CaraInicial;
        //        else
        //            CaraEncuestada += 1;
                         

        //        //Encuesta caras Activas para determinar estado y toma accion sobre el estado si no hay error en los datos durante la comunicacion
        //        if (EstructuraRedSurtidor[CaraEncuestada].Activa == true &&
        //            EstructuraRedSurtidor[CaraEncuestada].Cara <= 16)
        //            EncuestarCara();
        //        else if (EstructuraRedSurtidor[CaraEncuestada].Cara > 16)
        //        {
        //            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Error|Fuera de rango permitido por Protocolo. Numero de cara de surtidor: " +
        //                        EstructuraRedSurtidor[CaraEncuestada].Cara);
        //            SWRegistro.Flush();
        //        }
                
        //        //Luego de realizado el proceso se reactiva el Timer
        //        PollingTimer.Start();
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepción en el Evento PollingTimerEvent: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
        //        SWRegistro.Flush();

        //    }
        //}

      

        //private void oEvento_VentaAutorizada( byte Cara,  string Precio,  string ValorProgramado,  byte TipoProgramacion,  string Placa)
        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
      
        {
            try
            {
                //if (CaraInicial <= Cara && Cara <= NumerodeCaras)
                if (EstructuraRedSurtidor.ContainsKey(Cara))                
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Evento|Recibe Autorización. Valor Programado " + ValorProgramado +
                        " - Tipo de Programación: " + TipoProgramacion);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para desapchar
                    //AutorizarCara[Cara] = true;
                    EstructuraRedSurtidor[Cara].AutorizarCara = true;

                    //ValorPredeterminado[Cara] = Convert.ToDecimal(ValorProgramado);
                    EstructuraRedSurtidor[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    //Si viene valor para predeterminar setea banderas
                    //if (ValorPredeterminado[Cara] != 0)
                    if (EstructuraRedSurtidor[Cara].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            //PredeterminarVolumen[Cara] = true;
                            EstructuraRedSurtidor[Cara].PredeterminarVolumen = true;
                            //PredeterminarValor[Cara] = false;
                            EstructuraRedSurtidor[Cara].PredeterminarImporte = false;
                        }
                        else
                        {
                            //PredeterminarVolumen[Cara] = false;
                            EstructuraRedSurtidor[Cara].PredeterminarVolumen = false;
                            //PredeterminarValor[Cara] = true;
                            EstructuraRedSurtidor[Cara].PredeterminarImporte = true;
                        }
                    }
                    else
                    {
                        //PredeterminarVolumen[Cara] = false;
                        EstructuraRedSurtidor[Cara].PredeterminarVolumen = false;
                        //PredeterminarValor[Cara] = false;
                        EstructuraRedSurtidor[Cara].PredeterminarImporte = false;
                        //Valor de programación de la cara
                        //ValorPredeterminado[Cara - 1] = Convert.ToDouble(ValorProgramado);
                        //Tipo de programación (m3 o $)
                        //PredeterminarVolumen[Cara - 1] = false;// Convert.ToDouble(TipoProgramacion);
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|fuera de red de surtidores. Método: oEvento_VentaAutorizada");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_VentaAutorizada: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //private void oEvento_TurnoAbierto( string Surtidores,  string PuertoTerminal,  System.Array Precios)       
        ////private void oEvento_TurnoAbierto( string Surtidores,  string PuertoTerminal,  string Precio)
        //{
        //    try
        //    {
        //        IniciaTomaLecturasTurno(Surtidores, true);  //Indica que las lecturas a tomar son las iniciales
        //        PrecioEDS = Convert.ToDecimal(Precio);  //Asigna el nuevo precio
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepción en el Evento oEvento_TurnoAbierto: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
        //        SWRegistro.Flush();
        //    }
        //}

        public void Evento_TurnoAbierto(string Surtidores, string PuertoTerminal, System.Array Precios)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|Evento|Recibido (TurnoAbierto). Surtidores: " + Surtidores);
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
                    {
                        Productos.Add(PrecioProducto.IdProducto, PrecioProducto);


                        SWRegistro.WriteLine(DateTime.Now + "|IdProducto " + PrecioProducto.IdProducto + "|PrecioNivel1 = " + PrecioProducto.PrecioNivel1);
                        SWRegistro.Flush();// Borrar
                    }

                    else
                    {
                        Productos[PrecioProducto.IdProducto].PrecioNivel1 = PrecioProducto.PrecioNivel1;
                        Productos[PrecioProducto.IdProducto].PrecioNivel2 = PrecioProducto.PrecioNivel2;
                    }
                }



                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;
                //byte CaraTmp;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        //CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);
                        CaraLectura = Convert.ToByte((Convert.ToInt16(bSurtidores[i]) * 2) - 1);

                        //CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaAperturaTurno = true;
                                //Loguea evento
                                SWRegistro.WriteLine(DateTime.Now + "|Evento|Activar TomarLecturaAperturaTurno");
                                SWRegistro.Flush();

                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            EstructuraRedSurtidor[CaraLectura].ListaGrados[0].PrecioNivel1 =
                                Productos[EstructuraRedSurtidor[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel1;
                            EstructuraRedSurtidor[CaraLectura].ListaGrados[0].PrecioNivel2 =
                                Productos[EstructuraRedSurtidor[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel2;


                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|PrecioNivel1 = " + EstructuraRedSurtidor[CaraLectura].ListaGrados[0].PrecioNivel1);
                            SWRegistro.Flush();// Borrar

                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        //CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);
                        CaraLectura = Convert.ToByte((Convert.ToInt16(bSurtidores[i]) * 2));
                        

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        //CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaAperturaTurno = true;
                                //Loguea evento
                                SWRegistro.WriteLine(DateTime.Now + "|Evento|Activar TomarLecturaAperturaTurno");
                                SWRegistro.Flush();

                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            EstructuraRedSurtidor[CaraLectura].ListaGrados[0].PrecioNivel1 =
                                Productos[EstructuraRedSurtidor[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel1;
                            EstructuraRedSurtidor[CaraLectura].ListaGrados[0].PrecioNivel2 =
                                Productos[EstructuraRedSurtidor[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel2;


                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|PrecioNivel1 = " + EstructuraRedSurtidor[CaraLectura].ListaGrados[0].PrecioNivel1);
                            SWRegistro.Flush();// Borrar

                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }
                    }
                }


                IniciaTomaLecturasTurno(Surtidores, true);  //Indica que las lecturas a tomar son las iniciales
                //PrecioEDS = Convert.ToDecimal(Precio);  //Asigna el nuevo precio



            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        public void Evento_TurnoCerrado(string Surtidores, string PuertoTerminal)
        {
            try
            {

                SWRegistro.WriteLine(DateTime.Now + "|Evento|Recibido (TurnoCerrado). Surtidores: " + Surtidores); //DCF 11/04/2018
                SWRegistro.Flush();

                IniciaTomaLecturasTurno(Surtidores, false); //Indica que las lecturas a tomar son las finales     
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //Evento que manda a cambiar el producto y su respectivo precio en las mangueras
        public void Evento_ProgramarCambioPrecioKardex( ColMangueras mangueras)
        {
            try
            {
                //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
                foreach (Manguera OManguera in mangueras)
                {
                    foreach (RedSurtidor ORedSurtidor in EstructuraRedSurtidor.Values)
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
                                SWRegistro.WriteLine(DateTime.Now + "|" + ORedSurtidor.CaraBD + "|Manguera: " + OGrado.MangueraBD +
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
                      
        public void Evento_CerrarProtocolo()
        {
            this.CondicionCiclo = false;
        }


        public void Evento_FinalizarVentaPorMonitoreoCHIP(byte Cara)
        {       

        }
        
        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto)
        {
        }

        public void Evento_FinalizarCambioTarjeta(byte Cara)
        {            
        }

        #endregion


        public void Evento_CancelarVenta(byte Cara)
        {

        } 

        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
        }


        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera
        {
        }

    }
}