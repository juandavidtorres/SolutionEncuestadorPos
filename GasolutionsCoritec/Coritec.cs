using System;
using System.Collections.Generic;
using System.Text;
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using System.Globalization;     //Para manejo universal de la separación de miles y decimales
//using gasolutions;
using POSstation.Protocolos;
using System.Net.Sockets;
using System.Net;

    
//namespace gasolutions.Protocolos.Coritec
namespace POSstation.Protocolos
{
    public class Coritec:iProtocolo
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


        #region DECLARACIÓN DE VARIABLES
                //DECLARACIÓN DE OBJETOS
        string ArchivoRegistroSucesos;      //Variable que almacen la ruta y el nombre del archivo que guarda registro de los sucesos ocurrido en la cara
        StreamWriter SWRegistro;            //Variable utilizada para escribir en el archivo

        string ArchivoTramas;               //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        StreamWriter SWTramas;              //Variable utilizada para escribir en el archivo

        //SharedEventsFuelStation.CMensaje oEvento;     //Controla la comunicacion entre las aplicaciones por medio de eventos


        SerialPort PuertoCom = new SerialPort();              //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES  

        Dictionary<byte, RedSurtidor> PropiedadesCara;        //Diccionario donde se almacenan las Caras y sus propiedades

        //VARIABLES DE CONTROL
        enum ComandoSurtidor                //Define los posibles COMANDOS que se envian al Surtidor
        {
            RealTime,                       //R->Estado, Totalizador, Valor, Volumen, Precio 
            EstablecerPrecio,               //P!
            ObtenerPrecio,                  //P?
            EstablecerDensidad,             //D!
            ObtenerDensidad,                //D?
            Version,                        //N?
            Aforador,                        //C
            Autorizar,
            NegarAutorizacion,
            LeerROM,
            Desbloqueo, //b Utilizado en Coritec 2.
            PredeterminacionVolumen,
            PredeterminacionImporte,
            Anulapredeterminación,

        }
        ComandoSurtidor ComandoCaras;       //Almacena el comando a ser enviado

        bool FalloComunicacion;             //Establece si hubo error en la comunicación (Surtidor no contesta)
        byte CaraEncuestada;                //Cara que se está siendo encuestada
        bool CondicionCiclo = true;
        //VARIABLES PARA USO ESPECÍFICO
        decimal PrecioEDS;
        string PrecioEDSString;//Almacena el PRECIO vigente en la EDS
        string DensidadEDS; //DCF
        decimal VolumenPredeterminado;
        decimal ImportePredeterminado;

        /*Tramas compuestas de bytes para comunicacion con SURTIDOR */
        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR  

        int TimeOut;

         //TCPIP

        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        public string Puerto;
        int Bytes_leidos;
        int BytesRecibidos = 0;

        TcpClient ClienteCoritec;
        NetworkStream Stream;


        AsyncCallback callBack = new AsyncCallback(CallBackMethod);

        //VARIABLES TEMPORALES PARA EFECTO PRUEBA

     
        bool CondicionCiclo2 = true;
        bool EncuentaFinalizada = false;

        byte CaraTmp; // Utilizado para las caras con alias mas de 16 caras

        byte CaraID;//DCF Alias 

        #endregion

        //public Coritec(string Puerto, byte NumerodeCaras, byte CaraInicial, string strPrecioEDS, List<Cara> ListaPropiedadesCara)
        public Coritec(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
                this.Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-SucesosCoritec (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-TramasCoritec (" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Escribe encabezado
                SWRegistro.WriteLine("===================|==|======|=====================================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.07.12-15468");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.14 - 1135 ");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.18 - 0455 ");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.27 - 1025"); // Aplicacion para FullSatations 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.04.04 - 1816"); //Corregido Convert.ToByte(CamposRx[1]) --,16 mas de 10 caras
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.04.20 - 1530"); //Estado = 1 y 2 Manguera descolgada, CoritecDescolgadaAutorizada  por CoritecDescolgada;
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.05.02 - 0957"); //Estado = 1 y 2 Manguera descolgada, CoritecDescolgadaAutorizada  por CoritecDescolgada; Error en einicio de protocolo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.05.13- 1700"); //predeterminación por importe y volumen y firmware
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 19.04.2012- 1207"); //Cambio de precio con idproducto =1
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 05.07.2013- 1803"); // Para Evitar que se pida una autorizacion sin terminar la venta anterior DCF 05/07/2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 28.11.2013- 1025");//Environment.CurrentDirectory  por  Application.StartupPath 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 13.09.2014- 1750");//TimeOut
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 29.03.2016- 1828");//PrecioProducto.IdProducto == 1 y //para mexico debe ser .0 y para colombia y peru ,0
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec -COM modificado 10.07.2017- 1227");//TCP
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 08.03.2018- 1634");//DCF Archivos .txt 08/03/2018  
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 13.04.2018 - 1158 ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 18.04.2018 - 1442 ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                SWRegistro.Flush();

                ////Instancia los eventos disparados por la aplicacion cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                ////oEvento.ProgramarCambioPrecioKardex += new SharedEventsFuelStation.__CMensaje_ProgramarCambioPrecioKardexEventHandler(oEvento_ProgramarCambioPrecioKardex);
                ////oEvento.FinalizarVentaPorMonitoreoCHIP += new SharedEventsFuelStation.__CMensaje_FinalizarVentaPorMonitoreoCHIPEventHandler(oEvento_FinalizarVentaPorMonitoreoCHIP);
                //oEvento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEvento_CerrarProtocolo);
                //oEvento.CambiarDensidad += new SharedEventsFuelStation.__CMensaje_CambiarDensidadEventHandler(oEvento_CambiarDensidad);



                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.None;
                    PuertoCom.ReadBufferSize = 4096;
                    PuertoCom.WriteBufferSize = 4096;

                    //Abre el puerto
                    PuertoCom.Open();
                }

                //PropiedadesCara es la erencia con la que se va a trabajar
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;


                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                HiloCicloCaras.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Coritec " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                throw Excepcion;
            }
        }



        public Coritec(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {  
            
            try
            {
                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-SucesosCoritec (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-TramasCoritec (" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);


                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Puerto;



                if (EsTCPIP)
                {
                    try
                    {
                        //Crea y abre la conexión con el Servidor
                        ClienteCoritec = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClienteCoritec.GetStream();

                        SWRegistro.WriteLine(DateTime.Now + "|0|Conexion establecida |" + EsTCPIP + "- DireccionIP: " + DireccionIP + " - Puerto: " + Puerto);
                        SWRegistro.Flush();

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }

                }
                else
                {

                    if (!PuertoCom.IsOpen)
                    {
                        PuertoCom.PortName = Puerto;
                        PuertoCom.BaudRate = 9600;
                        PuertoCom.DataBits = 8;
                        PuertoCom.StopBits = StopBits.One;
                        PuertoCom.Parity = Parity.None;
                        PuertoCom.ReadBufferSize = 4096;
                        PuertoCom.WriteBufferSize = 4096;

                        //Abre el puerto
                        PuertoCom.Open();
                    }


                    SWRegistro.WriteLine(DateTime.Now + "|0|EsTCPIP|" + EsTCPIP);
                    SWRegistro.Flush();
                    
                }


                //Escribe encabezado
                SWRegistro.WriteLine("===================|==|======|=====================================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.07.12-15468");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.14 - 1135 ");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.18 - 0455 ");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2010.08.27 - 1025"); // Aplicacion para FullSatations 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.04.04 - 1816"); //Corregido Convert.ToByte(CamposRx[1]) --,16 mas de 10 caras
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.04.20 - 1530"); //Estado = 1 y 2 Manguera descolgada, CoritecDescolgadaAutorizada  por CoritecDescolgada;
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.05.02 - 0957"); //Estado = 1 y 2 Manguera descolgada, CoritecDescolgadaAutorizada  por CoritecDescolgada; Error en einicio de protocolo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2011.05.13- 1700"); //predeterminación por importe y volumen y firmware
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 19.04.2012- 1207"); //Cambio de precio con idproducto =1
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 05.07.2013- 1803"); // Para Evitar que se pida una autorizacion sin terminar la venta anterior DCF 05/07/2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 28.11.2013- 1025");//Environment.CurrentDirectory  por  Application.StartupPath 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 13.09.2014- 1750");//TimeOut
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec modificado 29.03.2016- 1828");//PrecioProducto.IdProducto == 1 y //para mexico debe ser .0 y para colombia y peru ,0
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 10.07.2017- 1732 *");//TCP
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 12.07.2017- 1713 **");//TCP
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 08.03.2018- 1634**");//DCF Archivos .txt 08/03/2018  
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 13.04.2018 - 1158**");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Coritec TCP_IP modificado 18.04.2018 - 1442 ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
              
                SWRegistro.Flush();


                //EstructuraRedSurtidor es la erencia con la que se va a trabajar
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;


                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                HiloCicloCaras.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Coritec " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                throw Excepcion;
            }
        }


       

        //CICLO INFINITO DE RECORRIDO DE LAS CARAS (REEMPLAZO DEL TIMER)
        private void CicloCara()
        {
            try
            {
                //Variable para garantizar el ciclo infinito
                this.CondicionCiclo = true;
                int cont = 0;
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

                                EncuentaFinalizada = false;

                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                if (ProcesoEnvioComando(ComandoSurtidor.RealTime))
                                    TomarAccion();

                                EncuentaFinalizada = true;
                            }
                            else
                            {

                                if (cont < 5)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Cara Desactivada|");
                                    SWRegistro.Flush(); //logueo cara desactivada 19/06/2012                              
                                    cont++;
                                }


                            }
                        }

                    }
                }
            }
            catch (Exception Excepcion)
            {

                EncuentaFinalizada = true;
                CondicionCiclo2 = true;

                string MensajeExcepcion = "Excepción en el Método CicloCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        private bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 5;//Antes 5

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa el vector de control de fallo de comunicación
                FalloComunicacion = false;

                //Arma la trama de Transmision
                ArmarTramaTx(ComandoaEnviar);

                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {
                    if (EsTCPIP)
                        EnviarComando_TCPIP();
                    else
                        EnviarComando();


                    if (EsTCPIP)
                        RecibirInformacion_TCPIP();
                    else
                        RecibirInformacion();

                    Reintentos += 1;
                } while (FalloComunicacion && (Reintentos < MaximoReintento));

                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion)
                {
                    //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                    if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {
                        PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        string MensajeErrorLectura = "Error en Comunicacion con Surtidor";
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            bool EstadoTurno = false;
                            PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                    }

                    //Ingresa a este condicional si el surtidor NO responde y si no se ha logueado aún la falla
                    if (!PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Perdida de comunicacion. Estado: " + PropiedadesCara[CaraEncuestada].Estado +
                           " - Comando enviado: " + ComandoaEnviar);
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
                string MensajeExcepcion = "Excepción en el Método ProcesoEnvioComando: " + Excepcion;
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
                string strTramaTx = "";
                ComandoCaras = ComandoTx;
                TimeOut = 400;
                switch (ComandoTx)
                {
                    case ComandoSurtidor.RealTime:
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|R|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'?';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;

                    case ComandoSurtidor.ObtenerPrecio: // CONSULTA DE PRECIO UNITARIO TERMINA CON ?
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|P|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'?';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;


                    case ComandoSurtidor.EstablecerPrecio: // SETEO DE PRECIO UNITARIO TERMINA CON !

                        //string Precio = Convert.ToString(PrecioEDS);
                        string Precio = Convert.ToString(PrecioEDSString);

                        if (!Precio.Contains(",") && !Precio.Contains("."))//DCF el surtidor no reconoce el formato si el precio no contiene "," ó "." 
                        {
                            Precio = Precio + ".0";
                        }else if (Precio.Contains(",")) //para mexico debe ser .0 y para colombia y peru ,0
                        {
                            Precio = Precio.Replace(",", ".");

                        }
                        Precio = Precio.PadRight(5, '0');

                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|P|" + Precio + "|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;

                    case ComandoSurtidor.Aforador:
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|C|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'?';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;

                    case ComandoSurtidor.Autorizar:
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|W|3|27|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;

                    case ComandoSurtidor.NegarAutorizacion:
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada) + "|W|7|27|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;

                    case ComandoSurtidor.LeerROM:
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|W|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'?';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;

                    case ComandoSurtidor.ObtenerDensidad: //D?
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|D|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'?';
                        break;

                    case ComandoSurtidor.EstablecerDensidad: //D!

                        if (!DensidadEDS.Contains(",") && !DensidadEDS.Contains("."))//DCF el surtidor no reconoce el formato si el precio no contiene "," ó "." 
                        {
                            DensidadEDS = DensidadEDS + ".0";
                        }
                        DensidadEDS = Convert.ToString(DensidadEDS.PadRight(5, '0')).Replace(',', '.');

                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|D|" + DensidadEDS + "|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';

                        PropiedadesCara[CaraEncuestada].CambiarDensidad = false; //DCF
                        break;

                    case ComandoSurtidor.Desbloqueo: //b
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|b|00|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;


                    case ComandoSurtidor.Version:
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|N|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'?';            //Consultar la Version del Surtidor 
                        break;



                    case ComandoSurtidor.PredeterminacionVolumen: //                       

                        string Volumenpred = Convert.ToString(PropiedadesCara[CaraEncuestada].ValorPredeterminado.ToString("N2"));

                        if (!Volumenpred.Contains(",") && !Volumenpred.Contains("."))//DCF el surtidor no reconoce el formato si el precio no contiene "," ó "." 
                        {
                            Volumenpred = Volumenpred + ",00";
                        }

                        //Volumenpred = Volumenpred.PadRight(7, '0');
                        VolumenPredeterminado = Convert.ToDecimal(Volumenpred);  //ToDecimal la "," pasa a "." pero el al utilizar el"." pasa el dato como entero             
                        string VOL = Convert.ToString(VolumenPredeterminado).Replace(',', '.');

                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|j|" + VOL + "|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;




                    case ComandoSurtidor.PredeterminacionImporte:

                        string Importepred = Convert.ToString(PropiedadesCara[CaraEncuestada].ValorPredeterminado.ToString("N2"));

                        if (!Importepred.Contains(",") && !Importepred.Contains("."))//DCF el surtidor no reconoce el formato si el precio no contiene "," ó "." 
                        {
                            Importepred = Importepred + ",00";
                        }

                        //Volumenpred = Volumenpred.PadRight(7, '0');
                        ImportePredeterminado = Convert.ToDecimal(Importepred);  //ToDecimal la "," pasa a "." pero el al utilizar el"." pasa el dato como entero             
                        string IMpor = Convert.ToString(ImportePredeterminado).Replace(',', '.');

                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|i|" + IMpor + "|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';            //se Agrega el Terminador de tipo Byte a la Trama a Enviar.
                        break;



                    case ComandoSurtidor.Anulapredeterminación:
                        strTramaTx = ":" + Convert.ToString(CaraEncuestada, 16) + "|h|0|%";
                        TramaTx = new byte[strTramaTx.Length + 3];
                        TramaTx[TramaTx.Length - 1] = (byte)'!';
                        break;







                }

                //Se crea un vector temporal a partir de la cadena strTramaTx
                byte[] TramaTxTemporal = Encoding.ASCII.GetBytes(strTramaTx);

                //Se termina de cargar el arreglo de bytes a enviar
                for (int i = 0; i < TramaTxTemporal.Length; i++)
                    TramaTx[i] = TramaTxTemporal[i]; // Arma la Trama del Comando a Enviar 

                byte[] CRCTX = CalcularCRC(strTramaTx);// Llama la Subrutina de calculo del CRC

                //Se Agrega el 1er Byte del CRC
                TramaTx[TramaTx.Length - 3] = CRCTX[CRCTX.Length - 2];

                //Se Agrega el 2° Byte del CRC
                TramaTx[TramaTx.Length - 2] = CRCTX[CRCTX.Length - 1];
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ArmarTramaTx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ENVIA EL COMANDO AL SURTIDOR

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
                //string strTrama = "";
                //for (int i = 0; i <= TramaTx.Length - 1; i++)
                //    strTrama += TramaTx[i].ToString("X2") + "|";


                SWTramas.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                    "|" + CaraEncuestada + "|Tx-|" +  Encoding.Default.GetString(TramaTx));

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
                    "|" + CaraEncuestada + "|Tx|" + Encoding.Default.GetString(TramaTx));
                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////

                //Tiempo muerto mientras el Surtidor Responde
                Thread.Sleep(TimeOut);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método EnviarComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        public void VerifySizeFile() //Logueo
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-TramasCoritec (" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }

                FileInf = new FileInfo(ArchivoRegistroSucesos);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-SucesosCoritec (" + Puerto + ").txt";
                    SWRegistro = File.AppendText(ArchivoRegistroSucesos);
                }
            }

            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción Verificación del tamaño de Logueo: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }


        //LEE Y ALMACENA LA TRAMA RECIBIDA

        public void RecibirInformacion_TCPIP()
        {
            try
            {              
                byte[] TramaRxTemporal = new byte[255];

                if (!Stream.DataAvailable)
                    Thread.Sleep(40);

                if (Stream.DataAvailable)
                {
                    if (Stream.CanRead)
                    {
                        do
                        {
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                        } while (Stream.DataAvailable);
                    }
                                       
                        LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                        FalloComunicacion = false;


                        //Definicion de Trama Temporal
                        byte[] TramaTemporal = new byte[Bytes_leidos];

                        ////Almacena informacion en la Trama Temporal para luego eliminarle el eco
                        // PuertoCom.Read(TramaTemporal, 0, Bytes_leidos);
                        //PuertoCom.DiscardInBuffer();

                        //Se dimensiona la Trama a evaluarse (TramaRx)
                        TramaRx = new byte[TramaTemporal.Length];

                        //Almacena los datos reales (sin eco) en TramaRx
                        for (int i = 0; i <= (TramaTemporal.Length  - 1); i++)
                            TramaRx[i] = TramaRxTemporal[i];


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
                  if (ComprobarIntegridadTrama())
                        AnalizarTrama();

                    
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

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                if (Bytes > 0)
                {
                    //Se dimensiona la Trama a evaluarse (TramaRx)
                    TramaRx = new byte[Bytes];

                    //Almacena informacion en la Trama Temporal para luego eliminarle el eco
                    PuertoCom.Read(TramaRx, 0, Bytes);
                    //TramaRx = new byte[17] { 0x3A, 0x35, 0x7C, 0x43, 0x7C, 0x30, 0x7C, 0x34, 0x33, 0x2E, 0x36, 0x35, 0x7C, 0x25, 0x20, 0xDB, 0x42 };
                    /////////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA RECIBIDA
                    string strTrama = "{ ";
                    for (int i = 0; i < TramaRx.Length - 1; i++)
                        strTrama += "0x" + TramaRx[i].ToString("X2") + ", ";

                    strTrama += ("0x" + TramaRx[TramaRx.Length - 1] + " }");

                    SWTramas.WriteLine(
                        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                        DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                        "|" + CaraEncuestada + "|Rx|" + Encoding.Default.GetString(TramaRx));// + "   " + strTrama);
                    SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////
                    if (ComprobarIntegridadTrama())
                        AnalizarTrama();
                }


                else
                {
                    FalloComunicacion = true;
                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No responde a comando " + ComandoCaras);
                    //SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //COMPRUEBA CONSISTENCIA DE LA TRAMA RECIBIDA CON EL COMANDO ENVIADO
        private bool ComprobarIntegridadTrama()
        {
            try
            {
                //Calcular CRC
                string strTramaRx = Encoding.Default.GetString(TramaRx, 0, TramaRx.Length - 3);
                byte[] CRCCalculado = CalcularCRC(strTramaRx);

                // DCF cuando se recive 0x00 en el crc remplzar por 0xff
                if (TramaRx[TramaRx.Length - 3] == 0x00)
                {
                    TramaRx[TramaRx.Length - 3] = 0xff;
                }

                if (TramaRx[TramaRx.Length - 2] == 0x00)
                {
                    TramaRx[TramaRx.Length - 2] = 0xff;
                }

                ///DCF



                if (CRCCalculado[0] == TramaRx[TramaRx.Length - 3] && CRCCalculado[1] == TramaRx[TramaRx.Length - 2])
                {
                    string[] CamposRx = strTramaRx.Split(new char[] { '|', ':' });
                    byte CaraRecibida = Convert.ToByte(CamposRx[1], 16); //Corregido Convert.ToByte(CamposRx[1]) --,16
                    //Comparación entre cara enviada y cara recibida
                    if (CaraEncuestada == CaraRecibida)
                    {
                        //Comparación entre Comando Enviado y Comando Recibido
                        if (TramaTx[3] == TramaRx[3] || TramaRx[3] == Convert.ToByte('C') || TramaRx[7] == Convert.ToByte('E'))
                            FalloComunicacion = false;
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Comando enviado " + Convert.ToChar(TramaTx[3]) +
                                ": " + ComandoCaras + " - Comando recibido: " + TramaRx[3]);
                            SWRegistro.Flush();
                            FalloComunicacion = true;
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Cara que responde: " + Convert.ToChar(TramaRx[1]));
                        SWRegistro.Flush();
                        FalloComunicacion = true;
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|CRC Errado en comando " + ComandoCaras);
                    SWRegistro.Flush();
                    FalloComunicacion = true;
                }
                return !FalloComunicacion;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ComprobarIntegridadTrama: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return true;
            }
        }

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void AnalizarTrama()
        {
            try
            {
                string strTramaRx = Encoding.ASCII.GetString(TramaRx, 0, TramaRx.Length - 3);
                string[] CamposRx = strTramaRx.Split(new char[] { '|' });

                //if (CamposRx.Length > 7 && CamposRx[7] == "E") WAL
                if (CamposRx.Length == 6 && CamposRx[3] == "E")// DCF
                {
                    //switch (CamposRx[9]) //Wal
                    switch (CamposRx[4]) // DCF Codigo de Error 
                    {
                        case "0":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error: 0 ok");
                            SWRegistro.Flush();
                            break;
                        case "1":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error: 1 Función no soportada");
                            SWRegistro.Flush();
                            break;
                        case "2":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error: 2 Función invalida");
                            SWRegistro.Flush();
                            break;
                        case "3":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:3 Error de CRC");
                            SWRegistro.Flush();
                            break;
                        case "4":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:4 Error de formato del argumento");
                            SWRegistro.Flush();
                            break;
                        case "5":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:5 Error longitud del argumento");
                            SWRegistro.Flush();
                            break;
                        case "6":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:6 Error en el valor del argumento");
                            SWRegistro.Flush();
                            break;
                        case "7":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:7 Función no soportada en éste estado");
                            SWRegistro.Flush();
                            break;
                        case "8":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:8 Función redundante");
                            SWRegistro.Flush();
                            break;
                        case "9":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:9 Terminador invalido para esta función");
                            SWRegistro.Flush();
                            break;
                        case "A":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Error:A Error de CERO");
                            SWRegistro.Flush();
                            break;
                    }
                }
                else
                {
                    switch (CamposRx[1])
                    {
                        case "R":
                            AsignarEstado(CamposRx[2]); //Recuperar Estado:
                            PropiedadesCara[CaraEncuestada].Lectura = Convert.ToDecimal(CamposRx[3], CultureInfo.InvariantCulture);
                            PropiedadesCara[CaraEncuestada].TotalVenta = Convert.ToDecimal(CamposRx[4], CultureInfo.InvariantCulture);
                            PropiedadesCara[CaraEncuestada].Volumen = Convert.ToDecimal(CamposRx[5], CultureInfo.InvariantCulture);
                            PropiedadesCara[CaraEncuestada].PrecioVenta = Convert.ToDecimal(CamposRx[6], CultureInfo.InvariantCulture);
                            break;

                        case "C":
                            PropiedadesCara[CaraEncuestada].Lectura = Convert.ToDecimal(CamposRx[3], CultureInfo.InvariantCulture);
                            AsignarEstado(CamposRx[2]);
                            break;

                        case "P":
                            PropiedadesCara[CaraEncuestada].PrecioCara = Convert.ToDecimal(CamposRx[3], CultureInfo.InvariantCulture);

                            break;

                        case "M":
                            if (Convert.ToInt16(CamposRx[2]) != 0x03)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Autorizacion NO aceptada");
                                SWRegistro.Flush();
                            }
                            break;


                        case "b":
                            AsignarEstado(CamposRx[2]); //Recuperar Estado.
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Desbloqueo Aceptado Por Cara Surtidor");
                            SWRegistro.Flush();
                            break;



                        case "N":
                            PropiedadesCara[CaraEncuestada].VersionFirmware = Convert.ToDecimal(CamposRx[3], CultureInfo.InvariantCulture);
                            AsignarEstado(CamposRx[2]);

                            //:4|N|0|12082532|%|B*
                            break;


                        case "D":
                            PropiedadesCara[CaraEncuestada].Densidad = Convert.ToDecimal(CamposRx[3], CultureInfo.InvariantCulture);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Densidad Actul = " + PropiedadesCara[CaraEncuestada].Densidad);
                            SWRegistro.Flush();
                            break;


                        case "j":
                            PropiedadesCara[CaraEncuestada].ValorPredeterminado = Convert.ToDecimal(CamposRx[3], CultureInfo.InvariantCulture);
                            break;

                        case "i":
                            PropiedadesCara[CaraEncuestada].ValorPredeterminado = Convert.ToDecimal(CamposRx[3], CultureInfo.InvariantCulture);
                            break;


                        case "h":
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Cancelación de predeterminación ");
                            SWRegistro.Flush();

                            break;




                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método AnalizarTrama: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void AsignarEstado(string cEstado)
        {
            try
            {
                //Almacena ultimo estado si este no es indeterminado
                if (PropiedadesCara[CaraEncuestada].Estado != PropiedadesCara[CaraEncuestada].EstadoAnterior)
                    PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;

                //Asigna el estado recibido
                switch (cEstado)
                {
                    case "0":
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                        else
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecReposo;
                        break;
                    case "1":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecDescolgada;
                        break;
                    case "2":
                        //PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecDescolgadaAutorizada;
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecDescolgada; // DCF correcion para Bolivia 20-04-2011
                        break;
                    case "3":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecDespachando;
                        break;
                    case "4":
                    case "5":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritectFinDeCarga;
                        break;
                    case "6":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecTimeOut;
                        break;
                    case "7":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecHaciendoCero;
                        break;
                    case "8":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecMenu;
                        break;
                    case "A":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecBateria;
                        break;
                    case "G":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecCeroFinalizado;
                        break;

                    case "O":
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecBloquedo;
                        break;

                    default:
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.Indeterminado;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Estado desconocido: " + cEstado +
                            " - Comando enviado: " + ComandoCaras);
                        SWRegistro.Flush();
                        break;
                }

                //Estado Adicional
                if (TramaRx[3] == 0x43)
                    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.CoritecLecturaAforador;

                //Se deja registro de cambio de estado en el archivo de texto
                if (PropiedadesCara[CaraEncuestada].Estado != PropiedadesCara[CaraEncuestada].EstadoAnterior)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|" + PropiedadesCara[CaraEncuestada].Estado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método AsignarEstado: " + Excepcion;
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
                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (PropiedadesCara[CaraEncuestada].Estado)
                {
                    case EstadoCara.CoritecReposo:
                        //Informa cambio de estado
                        if (PropiedadesCara[CaraEncuestada].Estado != PropiedadesCara[CaraEncuestada].EstadoAnterior)
                        {
                            int mangueraColgada = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;

                            //CaraEnReposo( CaraEncuestada);
                            CaraEnReposo( CaraEncuestada,  mangueraColgada);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara en Espera");
                            SWRegistro.Flush();

                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                        }

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            LecturaAperturaCierre();
                        }


                        if (PropiedadesCara[CaraEncuestada].CambiarDensidad == true)
                        {
                            ProcesoEnvioComando(ComandoSurtidor.ObtenerDensidad);

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
                            while (FalloComunicacion == true && Reintentos < 1);
                            PropiedadesCara[CaraEncuestada].CambiarDensidad = false;
                            //DCF no enviear mas de 3 veces el comando Establecer Densidad si el surtidor no responde a este comando 
                        }



                        // Proceso de obtener versión de software y hardware así como la fecha de fabricación
                        //************************************* *********************************************

                        if (PropiedadesCara[CaraEncuestada].VersionFirmware == 0)
                        {
                            if (ProcesoEnvioComando(ComandoSurtidor.Version))
                            {
                                //Logueo de Firmware del Surtidor
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Version del Firmware " + PropiedadesCara[CaraEncuestada].VersionFirmware);
                                SWRegistro.Flush();
                            }

                            else
                            {
                                //Logueo de Firmware del Surtidor
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|No Obtuvo Versión del Firmware ");
                                SWRegistro.Flush();

                                PropiedadesCara[CaraEncuestada].VersionFirmware = 1;
                            }

                        }



                        break;

                    case EstadoCara.CoritecDescolgada:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                            string MensajeErrorLectura = "Manguera descolgada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }
                        //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                        //if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                        //    PropiedadesCara[CaraEncuestada].AutorizarCara == false)

                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                            PropiedadesCara[CaraEncuestada].AutorizarCara == false &&                       
                            PropiedadesCara[CaraEncuestada].EsVentaParcial == false) // Para Evitar que se pida una autorizacion sin terminar la venta anterior DCF 05/07/2013 

                        {
                            
                            int IdProducto = PropiedadesCara[CaraEncuestada].ListaGrados[0].IdProducto;
                            int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                            string Lectura = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");
                            AutorizacionRequerida(CaraEncuestada, IdProducto, IdManguera, Lectura, "");
                            //AutorizacionRequerida( CaraEncuestada);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion");
                            SWRegistro.Flush();
                        }

                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                        {
                            //Obtiene la Lectura Inicial de la Venta
                            PropiedadesCara[CaraEncuestada].LecturaInicialVenta = PropiedadesCara[CaraEncuestada].Lectura;

                            //Se reportan lecturas iniciales
                            string strLecturasVolumen = PropiedadesCara[CaraEncuestada].LecturaInicialVenta.ToString("N3"); ;
                            LecturaInicialVenta( CaraEncuestada,  strLecturasVolumen);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Inicial de Venta: " + strLecturasVolumen);
                            SWRegistro.Flush();

                            ProcesoEnvioComando(ComandoSurtidor.LeerROM);



                            // ***************************** ****************************** ***************************
                            // Predeterminar Importe y Volumen

                            if (PropiedadesCara[CaraEncuestada].ValorPredeterminado > 0)
                            {
                                if (Predeterminar())
                                {
                                    //Envía comando de Autorización
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Autorizacion de Venta Predeterminada");
                                    SWRegistro.Flush();

                                    ProcesoEnvioComando(ComandoSurtidor.Autorizar);

                                    //Apaga bandera de autorización
                                    PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                                    //Enciende bandera de Venta en Curso
                                    PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                                }
                                else
                                {
                                    //Envía comando de Autorización
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Predeterminada No Aceptada");
                                    SWRegistro.Flush();


                                    PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                }

                            }

                            else
                            {
                                // ***************************** ****************************** ***************************

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Programacion de Venta");
                                SWRegistro.Flush();
                                //Se autoriza la venta
                                ProcesoEnvioComando(ComandoSurtidor.Autorizar);

                                //Apaga bandera de autorización
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                                //Enciende bandera de Venta en Curso
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = true;

                            }

                        }
                        break;

                    case EstadoCara.CoritecDespachando:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                            string MensajeErrorLectura = "Manguera en despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        //Apaga bandera de autorización
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                        //Enciende bandera de Venta en Curso
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = true;

                        string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                        string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");

                        //Informa parciales de venta
                        VentaParcial( CaraEncuestada,  strTotalVenta,  strVolumen);

                        break;

                    case EstadoCara.FinDespachoForzado:
                    case EstadoCara.CoritectFinDeCarga:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                            string MensajeErrorLectura = "Manguera en Fin de Despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                            ProcesoFindeVenta();

                        break;

                    case EstadoCara.CoritecLecturaAforador:
                        ProcesoEnvioComando(ComandoSurtidor.Aforador);
                        break;


                    case EstadoCara.CoritecBloquedo:
                        ProcesoEnvioComando(ComandoSurtidor.Desbloqueo);
                        break;

                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método TomarAccion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //PARA TOMAR LECTURAS DE APERTURA Y/O CIERRE DE TURNO
        private void LecturaAperturaCierre()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();

                string strLecturasVolumen = Convert.ToString(PropiedadesCara[CaraEncuestada].Lectura);

                //Lista de lecturas
                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                ////Almacena las lecturas en la lista
                //ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD) + "|" +
                //    Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura));

                ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].Lectura) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].PrecioNivel1)); //DCF
                //Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioSurtidorNivel1));



                //Convierte la colección de lecturas en un SystemArray para pasar a través del SharedEvents
                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                ArrayLecturas.CopyTo(LecturasEnvio);

                //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                {


                    //LecturaTurnoCerrado( CaraEncuestada,  strLecturasVolumen);
                    LecturaTurnoCerrado( LecturasEnvio);
                    PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Final Turno: " + strLecturasVolumen);
                    SWRegistro.Flush();
                }

                //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    LecturaTurnoAbierto( LecturasEnvio);
                    //LecturaTurnoAbierto( CaraEncuestada,  strLecturasVolumen);
                    PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Inicial Turno: " + strLecturasVolumen);
                    SWRegistro.Flush();

                    //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                    if (!CambiarPrecio())
                    {
                        string MensajeErrorLectura = "Cambio de precio fallido";

                        bool EstadoTurno = false;
                        CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en cambio de precio al inicio de turno: " + MensajeErrorLectura);
                        SWRegistro.Flush();
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //REALIZA PROCESO DE FIN DE VENTA
        private void ProcesoFindeVenta()
        {
            try
            {
                decimal VolumenCalculado = 0;
                decimal ValorCalculado = 0;

                //Valor de Lecturas Finales de Venta
                PropiedadesCara[CaraEncuestada].LecturaFinalVenta = PropiedadesCara[CaraEncuestada].Lectura;

                //Se Calcula el Volumen según Lecturas
                if (PropiedadesCara[CaraEncuestada].LecturaFinalVenta >= PropiedadesCara[CaraEncuestada].LecturaInicialVenta)
                {
                    VolumenCalculado = PropiedadesCara[CaraEncuestada].LecturaFinalVenta - PropiedadesCara[CaraEncuestada].LecturaInicialVenta;

                    if (PropiedadesCara[CaraEncuestada].Volumen < VolumenCalculado ||
                        PropiedadesCara[CaraEncuestada].Volumen > VolumenCalculado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Volumen Calculado: " + VolumenCalculado +
                            " - Volumen Reportado: " + PropiedadesCara[CaraEncuestada].Volumen);
                        SWRegistro.Flush();

                        if (VolumenCalculado == 0)
                        {
                            PropiedadesCara[CaraEncuestada].Volumen = 0;
                            PropiedadesCara[CaraEncuestada].TotalVenta = 0;
                        }

                        //PropiedadesCara[CaraEncuestada].Volumen = VolumenCalculado;
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura Inicial Mayor que Lectura Final");
                    SWRegistro.Flush();
                }

                //Se Calcular el Valor de la Venta según Volumen y Precio
                if (PropiedadesCara[CaraEncuestada].PrecioVenta > 0)
                {
                    ValorCalculado = PropiedadesCara[CaraEncuestada].Volumen * PropiedadesCara[CaraEncuestada].PrecioVenta;

                    if (PropiedadesCara[CaraEncuestada].TotalVenta != ValorCalculado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Valor Calculado: " + ValorCalculado +
                            " - Valor Reportado: " + PropiedadesCara[CaraEncuestada].TotalVenta);
                        SWRegistro.Flush();
                    }
                }

                if (PropiedadesCara[CaraEncuestada].Volumen > 0)
                {

                    //Dispara evento al programa principal si la venta es diferente de 0             
                    string Cara_Encuestada = CaraEncuestada.ToString(); 
                    string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                    string strPrecio = PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                    string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].LecturaFinalVenta.ToString("N3");
                    string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                    //byte bytProducto = 1;

                    //Asigna Valores de Fin de Venta para disparar evento
                    string strLecutrasVolumen = PropiedadesCara[CaraEncuestada].LecturaFinalVenta.ToString("N2"); //Convert.ToString(Totalizadores[CodigoSurtidor - 1][Linea]);
                    string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].LecturaInicialVenta.ToString("N3");// "0";
                    byte bytProducto = Convert.ToByte(PropiedadesCara[CaraEncuestada].ListaGrados[0].IdProducto);
                    int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                    String PresionLLenado = "0";


                    //Loguea evento Fin de Venta
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informar Finalizacion Venta. Importe: " + strTotalVenta +
                        " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                        " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                    SWRegistro.Flush();


                    VentaFinalizada(CaraEncuestada, strTotalVenta, strPrecio, strLecturaFinalVenta, strVolumen, Convert.ToString(bytProducto), IdManguera, PresionLLenado, strLecturaInicialVenta);

                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;

                    //string[] Args = { CaraEncuestada.ToString(), strTotalVenta.ToString(), strPrecio.ToString(), strLecturaFinalVenta.ToString(), strVolumen.ToString(), bytProducto.ToString(), IdManguera.ToString(), PresionLLenado.ToString(), strLecturaInicialVenta.ToString() };

              
                    //ThreadPool.QueueUserWorkItem(new WaitCallback(InformarFinalizacionVenta), Args);                

                  
                    
                    //VentaFinalizada( CaraEncuestada,  strTotalVenta,  strPrecio,  strLecturaFinalVenta,  strVolumen,  bytProducto,  PresionLlenado);
                    //VentaFinalizada( CaraEncuestada,  strTotalVenta,  strPrecio,  strLecutrasVolumen,  strVolumen, Convert.ToString(bytProducto),  Manguera,   PresionLLenado,  strLecturaInicialVenta);
        


                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fin de venta: Importe: " + strTotalVenta +
                    //    " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta + " - Volumen: " + strVolumen + " - Presión: " + PresionLlenado);
                    //SWRegistro.Flush();
                }
                else
                {
                    if (VentaInterrumpidaEnCero!=null)
                    {
                        VentaInterrumpidaEnCero(CaraEncuestada);
                        
                    }
                   
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Venta en CERO");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ProcesoFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }





        public void InformarFinalizacionVenta(object args)
        {
            string[] Argumentos = (string[])args;
            //{ CaraEncuestada.ToString(), strTotalVenta.ToString(), strPrecio.ToString(), strLecturaFinalVenta.ToString(), strVolumen.ToString(), bytProducto.ToString(), IdManguera.ToString(), PresionLLenado.ToString(), strLecturaInicialVenta.ToString() };

            byte CaraEncuestadaFinVenta = Convert.ToByte(Argumentos[0]);
            string strTotalVenta = Argumentos[1];
            string strPrecio = Argumentos[2];
            string strLecturaFinalVenta = Argumentos[3];
            string strVolumen = Argumentos[4];
            string bytProducto = Convert.ToString(Argumentos[5]);
            int IdManguera = Convert.ToInt32(Argumentos[6]);
            string PresionLLenado = Argumentos[7];
            string strLecturaInicialVenta = Argumentos[8];

            if (VentaFinalizada != null)
                {
                    VentaFinalizada(CaraEncuestadaFinVenta, strTotalVenta, strPrecio, strLecturaFinalVenta, strVolumen, bytProducto, IdManguera, PresionLLenado, strLecturaInicialVenta);
                }          

        }


        //CAMBIA EL PRECIO DE LA CARA
        private bool CambiarPrecio()
        {
            try
            {
                int Reintentos = 0;
                do
                {
                    //Obtiene el valor del precio seteado en la manguera
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                    {
                        if (PropiedadesCara[CaraEncuestada].PrecioCara != PrecioEDS)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Cambio de precio. Precio antiguo: " +
                                PropiedadesCara[CaraEncuestada].PrecioCara + " - Precio nuevo: " + PrecioEDS + ". Reintentos: " + Reintentos);
                            SWRegistro.Flush();

                            ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio);
                        }
                    }
                    Reintentos++;
                } while (PropiedadesCara[CaraEncuestada].PrecioCara != PrecioEDS && Reintentos < 3);

                if (PropiedadesCara[CaraEncuestada].PrecioCara != PrecioEDS)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Cambio de precio fallido");
                    SWRegistro.Flush();
                    return false;
                }
                else
                    return true;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método CambiarPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }


        public bool Predeterminar()
        {
            try
            {

                decimal ValorPredeterminado_Tem = 0;
                bool predeterminacio = false;
                // **************************  *************************
                // Predeterminar por Volumen 
                if (PropiedadesCara[CaraEncuestada].PredeterminarVolumen)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Predeterminación por Volumen = " +
                        PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                    SWRegistro.Flush();

                    ValorPredeterminado_Tem = PropiedadesCara[CaraEncuestada].ValorPredeterminado;

                    ProcesoEnvioComando(ComandoSurtidor.PredeterminacionVolumen);
                    if (ValorPredeterminado_Tem == PropiedadesCara[CaraEncuestada].ValorPredeterminado) //compara si se predetermino pior el valor enviado
                    {
                        predeterminacio = true;
                    }

                    else // Anula cualquier predeterminación,
                    {
                        ProcesoEnvioComando(ComandoSurtidor.Anulapredeterminación); //:1|h|0|%qı!
                        predeterminacio = false;
                    }
                }

                // **************************  *************************
                // Predeterminar por Importe
                if (PropiedadesCara[CaraEncuestada].PredeterminarImporte)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Predeterminación por Importe = " +
                       PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                    SWRegistro.Flush();

                    ValorPredeterminado_Tem = PropiedadesCara[CaraEncuestada].ValorPredeterminado;

                    ProcesoEnvioComando(ComandoSurtidor.PredeterminacionImporte);
                    if (ValorPredeterminado_Tem == PropiedadesCara[CaraEncuestada].ValorPredeterminado) //compara si se predetermino pior el valor enviado
                    {
                        predeterminacio = true;
                    }

                    else // Anula cualquier predeterminación,
                    {
                        ProcesoEnvioComando(ComandoSurtidor.Anulapredeterminación); //:1|h|0|%qı!
                        predeterminacio = false;
                    }
                }

                return predeterminacio;


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo Predeterminar: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }


        //CALCULA EL CARACTER DE REDUNDANCIA CICLICA
        private byte[] CalcularCRC(string Trama)
        {
            try
            {
                int CRC = 0xFFFF;
                int Carry;

                for (int i = 0; i < Trama.Length; i++)
                {
                    char Caracter = Convert.ToChar(Trama.Substring(i, 1));
                    CRC = CRC ^ (int)(Caracter);
                    for (int j = 1; j <= 8; j++)
                    {
                        Carry = CRC & 0x01;
                        CRC >>= 1;
                        if (Carry != 0)
                            CRC = CRC ^ 0xA001;
                    }
                }

                string sCRC = CRC.ToString("X2").PadLeft(4, '0'); //convierte el CRC de tipo INT a tipo String
                byte[] ArrayCRC = new byte[2];
                ArrayCRC[1] = Convert.ToByte(sCRC.Substring(0, sCRC.Length - 2), 16); //convierte el CRC a tipo Byte
                ArrayCRC[0] = Convert.ToByte(sCRC.Substring(sCRC.Length - 2, 2), 16);

                if (ArrayCRC[1] == 0x00)
                    ArrayCRC[1] = 0xFF;

                if (ArrayCRC[0] == 0x00)
                    ArrayCRC[0] = 0xFF;

                return ArrayCRC;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CalcularCRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                byte[] RetornoExcepcion = new byte[2];
                return RetornoExcepcion;
            }
        }

        #region EVENTOS RECIBIDOS DEL AUTORIZADOR
      public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
    // public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid)
        
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    //SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorización.");
                    //SWRegistro.Flush();

                    //Prueba
                    if (string.IsNullOrEmpty(ValorProgramado) || ValorProgramado==null)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + "Vacio" +
                     " - Tipo de Programacion: " + TipoProgramacion);
                        SWRegistro.Flush();

                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                    " - Tipo de Programacion: " + TipoProgramacion);
                        SWRegistro.Flush();
                    }


                    ValorProgramado = ValorProgramado.Trim();
                    string ValProgramado;

                    try
                    {
                       
                        //Manejo del precio de venta se debe enviar siempre. Para que funcione el cambio de precio DCF
                        if (Decimal.Truncate(Convert.ToDecimal(ValorProgramado)) == Convert.ToDecimal(ValorProgramado))
                        {
                            ValProgramado = Convert.ToInt64(ValorProgramado).ToString() + ".0";
                        }
                        else
                        {
                            ValProgramado = ValorProgramado.ToString().Replace(",", ".");
                        }
                    }
                    catch (Exception ex)
                    {
                        
                        string MensajeExcepcion = "Excepción en el Evento oEvento_VentaAutorizada-Programacion: " + ex.Message;
                        SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }
                   


                    PropiedadesCara[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|PropiedadesCara[Cara].ValorPredeterminado=" +  PropiedadesCara[Cara].ValorPredeterminado.ToString());
                     
                    /////

                    //PropiedadesCara[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado)/ PropiedadesCara[CaraEncuestada].FactorImporte;


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

                    //Bandera que indica que la cara debe autorizarse para desapchar
                    PropiedadesCara[Cara].AutorizarCara = true;


                  




                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_VentaAutorizada: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_TurnoAbierto( string Surtidores,  string PuertoTerminal,  System.Array Precios)
        {
            try
            {

                ////Diccionario de los precios manejados por cada manguera
                //Dictionary<int, Grados> Grados = new Dictionary<int, Grados>();

                ////Ciclo que arma Diccionario de Grados
                //foreach (string sPreciosManguera in Precios)
                //{
                //    //Objeto Grado para añadir al Diccionario
                //    Grados PrecioGrado = new Grados();

                //    string[] vPreciosGrado = sPreciosManguera.Split('|');
                //    PrecioGrado.IdProducto = Convert.ToByte(vPreciosGrado[0]);
                //    PrecioGrado.PrecioNivel1 = Convert.ToDecimal(vPreciosGrado[1]);
                //    PrecioGrado.PrecioNivel2 = Convert.ToDecimal(vPreciosGrado[2]);
                //    PrecioGrado.MangueraBD = Convert.ToInt16(vPreciosGrado[3]);

                //    //Si La Manguera no existe dentro del diccionario, lo añade
                //    if (!Grados.ContainsKey(PrecioGrado.MangueraBD))
                //    {
                //        Grados.Add(PrecioGrado.MangueraBD, PrecioGrado);

                //        string PrecioXX = Convert.ToString(Grados[PrecioGrado.MangueraBD].PrecioNivel1);

                //        PrecioEDS = (Grados[PrecioGrado.MangueraBD].PrecioNivel1);

                //        PrecioEDSString = PrecioXX.ToString().Replace(",", ".");

                //        SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|IdProducto = " + PrecioGrado.IdProducto + "|Precio de Producto Recibido = " + PrecioGrado.PrecioNivel1);
                //        SWRegistro.Flush();
                //    }
                //    else
                //    {
                //        Grados[PrecioGrado.MangueraBD].IdProducto = PrecioGrado.IdProducto;
                //        Grados[PrecioGrado.MangueraBD].PrecioNivel1 = PrecioGrado.PrecioNivel1;
                //        Grados[PrecioGrado.MangueraBD].PrecioNivel2 = PrecioGrado.PrecioNivel2;
                //    }

                //    //string PrecioXX = Convert.ToString(Grados[PrecioGrado.MangueraBD].PrecioNivel1);

                //    //PrecioEDS = (Grados[PrecioGrado.MangueraBD].PrecioNivel1);

                //    //PrecioEDSString = PrecioXX.ToString().Replace(",", ".");

                //}


                //Diccionario de los productos manejados
                //Dictionary<int, Producto> Productos = new Dictionary<int, Producto>();
                //Ciclo que arma Diccionario de Productos
                Producto PrecioProducto = new Producto();
                foreach (string sPreciosProducto in Precios)
                {
                    //Objeto Producto para añadir al Diccionario
                    string[] vPreciosProducto = sPreciosProducto.Split('|');
                    PrecioProducto.IdProducto = Convert.ToByte(vPreciosProducto[0]);
                    PrecioProducto.PrecioNivel1 = Convert.ToDecimal((vPreciosProducto[1]));
                    PrecioProducto.PrecioNivel2 = Convert.ToDecimal(vPreciosProducto[2]);

                    SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|IdProducto = " + PrecioProducto.IdProducto + "|Precio de Producto Recibido = " + vPreciosProducto[1]);
                    SWRegistro.Flush();

                    //if (PrecioProducto.IdProducto == 1)
                    //{
                    //    string PrecioXX = (vPreciosProducto[1]).Substring(0);

                    //    PrecioEDS = PrecioProducto.PrecioNivel1;

                    //    PrecioEDSString = PrecioXX.ToString().Replace(",", ".");
                    //}

                  
                    string PrecioXX = (vPreciosProducto[1]).Substring(0);
                    PrecioEDS = PrecioProducto.PrecioNivel1;
                    PrecioEDSString = PrecioXX.ToString().Replace(",", ".");
                    

                }

               
                IniciaTomaLecturasTurno(Surtidores, true);  //Indica que las lecturas a tomar son las iniciales    

                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Evento|Recibe evento de apertura de turno. " +
                "Surtidores: " + Surtidores + " - Precio: " + PrecioEDS);



                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Factores Configurados: ");
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|FactorVolumen = " + PropiedadesCara[CaraEncuestada].FactorVolumen);
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|FactorPrecio  = " + PropiedadesCara[CaraEncuestada].FactorPrecio);
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|FactorImporte = " + PropiedadesCara[CaraEncuestada].FactorImporte);
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|FactorTotalizador = " + PropiedadesCara[CaraEncuestada].FactorTotalizador); 



                SWRegistro.Flush();
            }

            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }




        public void Evento_TurnoCerrado( string Surtidores,  string PuertoTerminal)
        {
            try
            {
                IniciaTomaLecturasTurno(Surtidores, false); //Indica que las lecturas a tomar son las finales 
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Evento|Recibe evento de cierre de turno");
                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        private void oEvento_CambiarDensidad( string predDensidad)
        {
            try
            {
                foreach (RedSurtidor Propiedad in PropiedadesCara.Values)
                {
                    PropiedadesCara[Propiedad.Cara].CambiarDensidad = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Cambio de Densidad: " + predDensidad + ". Comando para Cambiar Densidad");
                    SWRegistro.Flush();
                }

                DensidadEDS = predDensidad;

                //Obtiene el separador decimal configurado en la CPU
                //string separador = System.Threading.Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;

                //if (predDensidad.Contains("."))
                //    predDensidad = predDensidad.Replace(".", separador);
                //else
                //    predDensidad = predDensidad.Replace(",", separador);

                //DensidadEDS = Convert.ToDecimal(predDensidad);              

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_CambiarDensidad: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                byte Cara = 1;
                foreach (RedSurtidor Propiedad in PropiedadesCara.Values)
                    PropiedadesCara[Propiedad.Cara].CambiarDensidad = false;
            }
        }


        public void VerificarConexion()
        {
            int iReintento = 0;
            string Comando = "";
            try
            {
                if (ClienteCoritec == null)
                {
                    Boolean EsInicializado = false;
                    while (!EsInicializado)
                    {
                        try
                        {
                            ClienteCoritec = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));

                            if (ClienteCoritec == null)
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

                        if (ClienteCoritec != null)
                        {
                            //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Inicializada - Ip: " + DireccionIP + " Puerto: " + Puerto);
                            //SWRegistro.Flush();
                            EsInicializado = true;
                        }
                    }
                }

                Boolean estadoAnterior = true;
                if (!this.ClienteCoritec.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClienteCoritec.Client.BeginDisconnect(true, callBack, ClienteCoritec);

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



                while (!this.ClienteCoritec.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteCoritec.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteCoritec);
                        //ClienteCoritec.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteCoritec.Client.Connected)
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

                this.Stream = ClienteCoritec.GetStream();

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
                //ClienteCoritec.Client.Disconnect(false);  
                ClienteCoritec.Client.Close();
                ClienteCoritec.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteCoritec = null;
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
                ClienteCoritec.Close();
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
                ClienteCoritec = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteCoritec.GetStream();
                if (this.ClienteCoritec.Client.Connected == true)
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


        #endregion

        #region MÉTODOS DEPENDIENTES DE EVENTOS RECIBIDOS
        //INICIALIZA VALORES DE LA MATRIZ PARA TOMA DE LECTURAS
        private void IniciaTomaLecturasTurno(string Surtidores, bool Apertura)
        {
            try
            {
                string[] bSurtidores = Surtidores.Split('|');
                int CaraLectura;
                for (int i = 0; i < bSurtidores.Length; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(bSurtidores[i]) * 2 - 1;

                        //Si la cara esta en la red
                        if (PropiedadesCara.ContainsKey(Convert.ToByte(CaraLectura)))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[Convert.ToByte(CaraLectura)].FalloTomaLecturaTurno = false;

                            if (Apertura)
                                PropiedadesCara[Convert.ToByte(CaraLectura)].TomarLecturaAperturaTurno = true;    //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                            else
                                PropiedadesCara[Convert.ToByte(CaraLectura)].TomarLecturaCierreTurno = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(bSurtidores[i]) * 2;

                        //Si la cara esta en la red
                        if (PropiedadesCara.ContainsKey(Convert.ToByte(CaraLectura)))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[Convert.ToByte(CaraLectura)].FalloTomaLecturaTurno = false;

                            if (Apertura)
                                PropiedadesCara[Convert.ToByte(CaraLectura)].TomarLecturaAperturaTurno = true;    //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                            else
                                PropiedadesCara[Convert.ToByte(CaraLectura)].TomarLecturaCierreTurno = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales

                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método IniciaTomaLecturasTurno: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        #endregion

        public void Evento_CerrarProtocolo()
        {
            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Recibe evento de detencion de Protocolo");
            SWRegistro.Flush();
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

        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
        }
        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)//Realizado por el remplazo del shared event, usa una interfaz en el proyecto Fabrica Protocolo
        {
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

                    //Si la cara esta activa se solicita la toma de lecturas en la apertura
                    if (PropiedadesCara[CaraLectura].Activa)//DCF19/01/2018
                    {

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {

                            if (PropiedadesCara[CaraTmp].Estado == EstadoCara.CoritecReposo)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {


                                while (!EncuentaFinalizada)
                                {

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                    Thread.Sleep(100);//21-02-2018 para que no bloquee el procesador en las tabletas de mexico
                                }

                                CaraEncuestada = CaraTmp;
                                CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias                           


                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();

                                TomarLecturas(); // obtener las lecturas de la cara en cuestion 

                                int i;
                                for (i = 0; i <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; i++)
                                {

                                    Lecturas += (Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD) + "|" +
                                    Convert.ToString(PropiedadesCara[CaraTmp].Lectura) + "|");

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                        PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD + " - Lectura " +
                                        PropiedadesCara[CaraTmp].Lectura);//DCF 
                                    SWRegistro.Flush();

                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                    //  PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD + " - Lectura " +
                                    //  PropiedadesCara[CaraTmp].ListaGrados[i].Lectura);
                                    //SWRegistro.Flush();
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
                            if (PropiedadesCara[CaraTmp].Estado == EstadoCara.CoritecReposo)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {

                                while (!EncuentaFinalizada)
                                {
                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                    Thread.Sleep(100);//21-02-2018 para que no bloquee el procesador en las tabletas de mexico
                                }

                                CaraEncuestada = CaraTmp;
                                CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();


                                TomarLecturas(); // obtener las lecturas de la cara en cuestion 

                                int i;
                                for (i = 0; i <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; i++)
                                {
                                    Lecturas += (Convert.ToString(PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD) + "|" +
                                    Convert.ToString(PropiedadesCara[CaraTmp].Lectura) + "|");

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                        PropiedadesCara[CaraTmp].ListaGrados[i].MangueraBD + " - Lectura " +
                                        PropiedadesCara[CaraTmp].Lectura);
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



                    CondicionCiclo2 = true; //Activa las encuenstas DCF 

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso: CondicionCiclo2 = " + CondicionCiclo2);
                    SWRegistro.Flush();

                }
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



        public void TomarLecturas()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;

                //Se resetea la lectura de cada grado de la cara
                for (int i = 0; i <= PropiedadesCara[CaraEncuestada].ListaGrados.Count - 1; i++)
                    PropiedadesCara[CaraEncuestada].ListaGrados[i].Lectura = 0;

                //Realiza hasta tres reintentos de toma de lecturas si hubo error en la obtención
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.RealTime))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Surtidor no respondio a comando Toma de Lectura en RealTime");
                        SWRegistro.Flush();
                    }
                    else
                        break;

                } while (Reintentos <= 3);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo TomarLecturas: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


    }
}
