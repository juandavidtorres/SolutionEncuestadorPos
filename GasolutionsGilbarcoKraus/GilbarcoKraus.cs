
using System;//Con Alias
using System.Collections.Generic;
//Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
//Para alcanzar la ruta de los ejecutables
using System.Windows.Forms;
using POSstation.Protocolos;
using System.Net.Sockets;
using System.Net;



namespace POSstation.Protocolos
{
    public class GilbarcoKraus : iProtocolo
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

        Dictionary<byte, RedSurtidor> EstructuraRedSurtidor;        //Diccionario donde se almacenan las Caras y sus propiedades

        //ENUMERACIONES UTILIZADA PARA CREAR VARIABLES        
        public enum ComandoSurtidor		//Define los posibles COMANDOS que se envian al Surtidor
        {
            Estado = 0x00,
            Autorizar = 0x10,
            EnviarDatos = 0x20,
            Detener = 0x30,
            TotalDespacho = 0x40,
            Totales = 0x50,
            ParcialDespacho = 0x60,
            DetenerTodos = 0xFC,
            //Trama para transmision de datos a la Cara, enviados despues del comando 0x02 (EnviarDatos)            
            CambiarPrecio,
            PredeterminarVentaDinero,
            PredeterminarVentaVolumen
        }

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        //SharedEventsFuelStation.CMensaje oEvento;
        System.Timers.Timer PollingTimer = new System.Timers.Timer(20); //Definicion del TIMER DE ENCUESTA
        ComandoSurtidor ComandoCaras;
        //EGV:Instancia Arreglo de lecturas para reportar reactivaci�n de cara
        System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int BytesEsperados;             //Declara la cantidad de bytes esperados por Comando
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO
        bool TramaEco;                  //Bandera que indica si dentro de la trama respuesta viene eco o no
        /*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        bool[] FalloComunicacion;      //Almacena el tipo de fallo de comunicacion        
        /*Tramas compuestas de bytes para comunicacion con SURTIDOR */
        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR       

        //Variable utilizada para escribir en el archivo
        string ArchivoTramas;
        StreamWriter SWTramas;

        AsyncCallback callBack = new AsyncCallback(CallBackMethod);

        string ArchivoRegistroSucesos;      //Variable que almacen la ruta y el nombre del archivo que guarda registro de los sucesos ocurrido en la cara
        StreamWriter SWRegistro;            //Variable utilizada para escribir en el archivo

        bool CondicionCiclo = true;

        bool ErrorComunicacion;         //Bandera que indica si hubo error en la comunicaci�n: Trama recibida con longitud 0 o incompleta

        int Bytes_leidos;
    
        byte CaraID;//DCF Alias 


        int MaximoReintento = 5;//2;//Antes 5 DCF reintentos 

        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        string Puerto;

        TcpClient ClienteGilbarco;


        NetworkStream Stream;

        #endregion

        #region METODOS PRINCIPALES

        //PUNTO DE ARRANQUE DE LA CLASE


        public GilbarcoKraus(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            
            try
            {

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + " GilbarcoKraus-RegistroSucesos (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);



                //Crea archivo para almacenar las tramas de transmisi�n y recepci�n (Comunicaci�n con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-GilbarcoKraus-Tramas(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);



                
                //Almacena en variables globales los par�metros de comunicaci�n
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Puerto;

               
                TramaEco = Eco;

                if (EsTCPIP)
                {
                    try
                    {
                        //Crea y abre la conexi�n con el Servidor
                        ClienteGilbarco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClienteGilbarco.GetStream();

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexi�n con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }


                }
                else if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 5760;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
                    PuertoCom.ReadBufferSize = 4096;
                    PuertoCom.WriteBufferSize = 4096;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception Excepcion)
                    {
                        string MensajeExcepcion = "No se pudo abrir puerto de comunicaci�n _ Configuraci�n TCPIP recibida: " + Excepcion;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                        throw Excepcion; //throw new Exception ("Comunicacion con surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }




                ////Crea archivo para almacenar inconsistencias en el proceso logico
                //string Archivo = Application.ExecutablePath + "GilbarcoKraus-RegistroSucesos(" + Puerto + ").txt";
                //SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine();
                SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2010.08.07 - 1000" );
                //SWRegistro.WriteLine("Archivo de inconsistencias, errores o saltos de logica en el programa");
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.04.08 - 1551"); //Logueo de Tramas
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.07.28 - 1800"); // PorReautorizar, lectura inicial y final. EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta, EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.08.18 - 1049");// Pruebas cambio precio chile
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.10.07 - 1550");// Predeterminacion  se multiplica por 100 esto para chile
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2011.10.08 - 0830");// 
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2011.11.26 - 1240");//  ALIAS 
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2012.01.23 - 1418");//  FactorPredeterminacionImporte =  10;  FactorPredeterminacionVolumen =100; para chile
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2012.03.02 - 1634");//  2012.03.02 - 1634.. DCF  //No enviar a ProcesoFindeVenta, se realiza el fin de venta cuando se cuelgue la manguera ******
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2012.03.03 - 1200");//  //Importe  y Volumen Calculado DCF 03/03/201
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 28-04-2012 - 1208");// //Modificado 28-04-2012 - 1208 -- log Lectura Inicial = Lectura Final
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 02-05-2012 - 1000");//si no se mueven los totalizadore los datos de venta son CERO //02-05-2012-1000
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 06-07-2012 - 1446");// DCF 06/07/2012 para bolivia en caso de no configurar el parametro FactorPredeterminacionImporte
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 09-07-2012 - 1702");// * EstructuraRedSurtidor[CaraEncuestada].FactorVolumen DCF 09/07/2012
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 19-02-2013 - 1807");   //11/03/2013
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 27-03-2013 - 1620");   //13/03/2013 cambio en la predeterminacon por importe  OK
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 28-11-2013 - 1052"); //Environment.CurrentDirectory  por  Application.StartupPath 
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 24-08-2015 - 1614");//JD 24/08/2015
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 29-12-2015 - 0947");//Para manejar la cara 16 Fecha: 29-12-2015
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 23-02-2016 - 1414");//para No cambiar en el configurador los surtidores que tiene 3 manguera pero solo despachan por 1 Para varias Mangueras 23-06-2015                       
                // SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS - TCP_IP.  Modificado 25-08-2016 - 1301");//DCF TCP-IP
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS - TCP_IP.  Modificado 31-05-2017 - 1642");//30/05/2017
                 //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS - TCP_IP.  Modificado 08-06-2017 - 1748");//08/06/2017
                 SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS - TCP_IP.  Modificado 22-06-2017 - 1305");//5 DCF reintentos 
                SWRegistro.WriteLine("=====================================================================");
                SWRegistro.Flush();


                ////Instancia los eventos disparados por la aplicacion cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                //oEvento.InactivarCaraCambioTarjeta += new SharedEventsFuelStation.__CMensaje_InactivarCaraCambioTarjetaEventHandler(oEvento_InactivarCaraCambioTarjeta);
                //oEvento.FinalizarCambioTarjeta += new SharedEventsFuelStation.__CMensaje_FinalizarCambioTarjetaEventHandler(oEvento_FinalizarCambioTarjeta);
                //oEvento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEvento_CerrarProtocolo);

             
                FalloComunicacion = new bool[2];
                /* [0] Error en Datos
                 * [1] Error en Comunicaci�n: Trama incompleta o no hay respuesta del surtidor*/



                //EstructuraRedSurtidor es la erencia con la que se va a trabajar
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                //foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                //{
                //    SW.WriteLine("================== CARA: " + ORedCaras.Cara + " ==================");
                //    SW.WriteLine("Factor Valor: " + ORedCaras.FactorImporte);
                //    SW.WriteLine("Factor Volumen: " + ORedCaras.FactorVolumen);
                //    SW.WriteLine("Factor Totalizador: " + ORedCaras.FactorTotalizador);
                //    SW.WriteLine("Factor Precio: " + ORedCaras.FactorPrecio);
                //    for (int j = 0; j <= ORedCaras.ListaGrados.Count - 1; j++)
                //    {
                //        SW.WriteLine("**************** Grado: " + j + " **********************");
                //        SW.WriteLine("Grado Programa: " + ORedCaras.ListaGrados[j].NoGrado);
                //        SW.WriteLine("Precio 1: " + ORedCaras.ListaGrados[j].PrecioNivel1);
                //        SW.WriteLine("Precio 2: " + ORedCaras.ListaGrados[j].PrecioNivel2);
                //        SW.WriteLine("Manguera: " + ORedCaras.ListaGrados[j].MangueraBD);
                //        SW.WriteLine("IdProducto: " + ORedCaras.ListaGrados[j].IdProducto);
                //    }
                //    SW.WriteLine("");
                //    SW.Flush();
                //}


                //DCF
                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                {
                    //foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                    //    SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                    //        " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1);

                    foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.CaraBD + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1); //Alias  oCara.CaraBD


                }
                SWRegistro.Flush();



              TramaEco = Eco;

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);
                //Inicial el hilo de encuesta c�clica
                HiloCicloCaras.Start();

                //Instancia los eventos de los objetos Timer
                //PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);

                //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
                //PollingTimer.AutoReset = true;

                //Se activa el timer por primera vez
                //PollingTimer.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el Constructor de la Clase Gilbarco";
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }




       public GilbarcoKraus(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                this.Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + " GilbarcoKraus-RegistroSucesos (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);



                //Crea archivo para almacenar las tramas de transmisi�n y recepci�n (Comunicaci�n con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-GilbarcoKraus-Tramas(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);




                ////Crea archivo para almacenar inconsistencias en el proceso logico
                //string Archivo = Application.ExecutablePath + "GilbarcoKraus-RegistroSucesos(" + Puerto + ").txt";
                //SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine();
                SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2010.08.07 - 1000" );
                //SWRegistro.WriteLine("Archivo de inconsistencias, errores o saltos de logica en el programa");
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.04.08 - 1551"); //Logueo de Tramas
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.07.28 - 1800"); // PorReautorizar, lectura inicial y final. EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta, EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.08.18 - 1049");// Pruebas cambio precio chile
                //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.10.07 - 1550");// Predeterminacion  se multiplica por 100 esto para chile
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2011.10.08 - 0830");// 
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2011.11.26 - 1240");//  ALIAS 
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2012.01.23 - 1418");//  FactorPredeterminacionImporte =  10;  FactorPredeterminacionVolumen =100; para chile
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2012.03.02 - 1634");//  2012.03.02 - 1634.. DCF  //No enviar a ProcesoFindeVenta, se realiza el fin de venta cuando se cuelgue la manguera ******
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2012.03.03 - 1200");//  //Importe  y Volumen Calculado DCF 03/03/201
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 28-04-2012 - 1208");// //Modificado 28-04-2012 - 1208 -- log Lectura Inicial = Lectura Final
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 02-05-2012 - 1000");//si no se mueven los totalizadore los datos de venta son CERO //02-05-2012-1000
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 06-07-2012 - 1446");// DCF 06/07/2012 para bolivia en caso de no configurar el parametro FactorPredeterminacionImporte
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 09-07-2012 - 1702");// * EstructuraRedSurtidor[CaraEncuestada].FactorVolumen DCF 09/07/2012
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 19-02-2013 - 1807");   //11/03/2013
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 27-03-2013 - 1620");   //13/03/2013 cambio en la predeterminacon por importe  OK
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 28-11-2013 - 1052"); //Environment.CurrentDirectory  por  Application.StartupPath 
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 24-08-2015 - 1614");//JD 24/08/2015
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 29-12-2015 - 0947");//Para manejar la cara 16 Fecha: 29-12-2015
                //  SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 23-02-2016 - 1414");//para No cambiar en el configurador los surtidores que tiene 3 manguera pero solo despachan por 1 Para varias Mangueras 23-06-2015                       
               // SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS - TCP_IP.  Modificado 31-05-2017 - 1642");//30/05/2017
                //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS - TCP_IP.  Modificado 08-06-2017 - 1748");//08/06/2017
                SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS - TCP_IP.  Modificado 22-06-2017 - 1305");//5 DCF reintentos 
                SWRegistro.WriteLine("=====================================================================");
                SWRegistro.Flush();


                ////Instancia los eventos disparados por la aplicacion cliente
                //Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEvento = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                //oEvento.InactivarCaraCambioTarjeta += new SharedEventsFuelStation.__CMensaje_InactivarCaraCambioTarjetaEventHandler(oEvento_InactivarCaraCambioTarjeta);
                //oEvento.FinalizarCambioTarjeta += new SharedEventsFuelStation.__CMensaje_FinalizarCambioTarjetaEventHandler(oEvento_FinalizarCambioTarjeta);
                //oEvento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEvento_CerrarProtocolo);

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 5760;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
                    PuertoCom.ReadBufferSize = 1024;
                    PuertoCom.WriteBufferSize = 1024;

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

                FalloComunicacion = new bool[2];
                /* [0] Error en Datos
                 * [1] Error en Comunicaci�n: Trama incompleta o no hay respuesta del surtidor*/



                //EstructuraRedSurtidor es la erencia con la que se va a trabajar
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                //foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                //{
                //    SW.WriteLine("================== CARA: " + ORedCaras.Cara + " ==================");
                //    SW.WriteLine("Factor Valor: " + ORedCaras.FactorImporte);
                //    SW.WriteLine("Factor Volumen: " + ORedCaras.FactorVolumen);
                //    SW.WriteLine("Factor Totalizador: " + ORedCaras.FactorTotalizador);
                //    SW.WriteLine("Factor Precio: " + ORedCaras.FactorPrecio);
                //    for (int j = 0; j <= ORedCaras.ListaGrados.Count - 1; j++)
                //    {
                //        SW.WriteLine("**************** Grado: " + j + " **********************");
                //        SW.WriteLine("Grado Programa: " + ORedCaras.ListaGrados[j].NoGrado);
                //        SW.WriteLine("Precio 1: " + ORedCaras.ListaGrados[j].PrecioNivel1);
                //        SW.WriteLine("Precio 2: " + ORedCaras.ListaGrados[j].PrecioNivel2);
                //        SW.WriteLine("Manguera: " + ORedCaras.ListaGrados[j].MangueraBD);
                //        SW.WriteLine("IdProducto: " + ORedCaras.ListaGrados[j].IdProducto);
                //    }
                //    SW.WriteLine("");
                //    SW.Flush();
                //}


                //DCF
                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                {
                    //foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                    //    SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                    //        " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1);

                    foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.CaraBD + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1); //Alias  oCara.CaraBD


                }
                SWRegistro.Flush();



                TramaEco = Eco;

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);
                //Inicial el hilo de encuesta c�clica
                HiloCicloCaras.Start();


                //Instancia los eventos de los objetos Timer
                //PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);

                //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
                //PollingTimer.AutoReset = true;

                //Se activa el timer por primera vez
                //PollingTimer.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el Constructor de la Clase Gilbarco";
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }


        //public GilbarcoKraus(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        //{
        //    try
        //    {

        //        if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
        //        {
        //            Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
        //        }


        //        //Crea archivo para almacenar inconsistencias en el proceso logico
        //        ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Gilbarco-Sucesos(" + Puerto + ").txt";
        //        SWRegistro = File.AppendText(ArchivoRegistroSucesos);

        //        ////Crea archivo para almacenar las tramas de transmisi�n y recepci�n (Comunicaci�n con Surtidor)
        //        ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Gilbarco-Tramas.(" + Puerto + ").txt";
        //        SWTramas = File.AppendText(ArchivoTramas);




        //        ////Crea archivo para almacenar inconsistencias en el proceso logico
        //        //string Archivo = Application.ExecutablePath + "GilbarcoKraus-RegistroSucesos(" + Puerto + ").txt";
        //        //SWRegistro = File.AppendText(Archivo);

        //        //Escribe encabezado en archivo de Inconsistencias
        //        SWRegistro.WriteLine();
        //        SWRegistro.WriteLine("=====================================================================");
        //        //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2010.08.07 - 1000" );
        //        //SWRegistro.WriteLine("Archivo de inconsistencias, errores o saltos de logica en el programa");
        //        //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.04.08 - 1551"); //Logueo de Tramas
        //        //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.07.28 - 1800"); // PorReautorizar, lectura inicial y final. EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta, EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta
        //        //SWRegistro.WriteLine(DateTime.Now + "GILBARCOKRAUS.  Modificado 2011.08.18 - 1049");// Pruebas cambio precio chile
        //        //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 2012.03.03 - 1200");//  //Importe  y Volumen Calculado DCF 03/03/201
        //        //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 02-05-2012 - 1000");//si no se mueven los totalizadore los datos de venta son CERO //02-05-2012-1000
        //        //SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 09-07-2012 - 1702");// * EstructuraRedSurtidor[CaraEncuestada].FactorVolumen DCF 09/07/2012                
        //        SWRegistro.WriteLine(DateTime.Now + " GILBARCOKRAUS.  Modificado 28-11-2013 - 1052"); //Environment.CurrentDirectory  por  Application.StartupPath 

        //        SWRegistro.WriteLine("=====================================================================");
        //        SWRegistro.Flush();




        //        ////Instancia los eventos disparados por la aplicacion cliente
        //        //oEvento = OEventoAutorizador;
        //        //oEvento.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
        //        //oEvento.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
        //        //oEvento.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
        //        //oEvento.InactivarCaraCambioTarjeta += new SharedEventsFuelStation.__CMensaje_InactivarCaraCambioTarjetaEventHandler(oEvento_InactivarCaraCambioTarjeta);
        //        //oEvento.FinalizarCambioTarjeta += new SharedEventsFuelStation.__CMensaje_FinalizarCambioTarjetaEventHandler(oEvento_FinalizarCambioTarjeta);
        //        //oEvento.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEvento_CerrarProtocolo);

        //        //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
        //        if (!PuertoCom.IsOpen)
        //        {
        //            PuertoCom.PortName = Puerto;
        //            PuertoCom.BaudRate = 5760;
        //            PuertoCom.DataBits = 8;
        //            PuertoCom.StopBits = StopBits.One;
        //            PuertoCom.Parity = Parity.Even;
        //            PuertoCom.ReadBufferSize = 1024;
        //            PuertoCom.WriteBufferSize = 1024;

        //            try
        //            {
        //                PuertoCom.Open();
        //            }
        //            catch (Exception ex)
        //            {
        //                throw ex; //throw new Exception ("Comunicacion con surtidor no disponible");
        //            }
        //            PuertoCom.DiscardInBuffer();
        //            PuertoCom.DiscardOutBuffer();
        //        }

        //        FalloComunicacion = new bool[2];
        //        /* [0] Error en Datos
        //         * [1] Error en Comunicaci�n: Trama incompleta o no hay respuesta del surtidor*/



        //        //EstructuraRedSurtidor es la erencia con la que se va a trabajar
        //        EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
        //        EstructuraRedSurtidor = EstructuraCaras;

        //        //foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
        //        //{
        //        //    SW.WriteLine("================== CARA: " + ORedCaras.Cara + " ==================");
        //        //    SW.WriteLine("Factor Valor: " + ORedCaras.FactorImporte);
        //        //    SW.WriteLine("Factor Volumen: " + ORedCaras.FactorVolumen);
        //        //    SW.WriteLine("Factor Totalizador: " + ORedCaras.FactorTotalizador);
        //        //    SW.WriteLine("Factor Precio: " + ORedCaras.FactorPrecio);
        //        //    for (int j = 0; j <= ORedCaras.ListaGrados.Count - 1; j++)
        //        //    {
        //        //        SW.WriteLine("**************** Grado: " + j + " **********************");
        //        //        SW.WriteLine("Grado Programa: " + ORedCaras.ListaGrados[j].NoGrado);
        //        //        SW.WriteLine("Precio 1: " + ORedCaras.ListaGrados[j].PrecioNivel1);
        //        //        SW.WriteLine("Precio 2: " + ORedCaras.ListaGrados[j].PrecioNivel2);
        //        //        SW.WriteLine("Manguera: " + ORedCaras.ListaGrados[j].MangueraBD);
        //        //        SW.WriteLine("IdProducto: " + ORedCaras.ListaGrados[j].IdProducto);
        //        //    }
        //        //    SW.WriteLine("");
        //        //    SW.Flush();
        //        //}

        //        TramaEco = Eco;

        //        //Crea el Hilo que ejecuta el recorrido por las caras
        //        Thread HiloCicloCaras = new Thread(CicloCara);
        //        //Inicial el hilo de encuesta c�clica
        //        HiloCicloCaras.Start();


        //        //Instancia los eventos de los objetos Timer
        //        //PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);

        //        //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
        //        //PollingTimer.AutoReset = true;

        //        //Se activa el timer por primera vez
        //        //PollingTimer.Start();
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepci�n en el Constructor de la Clase Gilbarco";
        //        SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
        //        SWRegistro.Flush();
        //    }
        //}



        private byte ConvertirCaraBD(byte caraBD) //YEZID Alias de las caras //DCF 2011-05-14
        {
            byte CaraSurtidor = 0;
            try
            {
                foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
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
                string MensajeExcepcion = "Excepci�n en funcion ConvertirCaraBD";
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();

            }
            return CaraSurtidor;
        }



        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        private bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                //Variable que indica el maximo numero de reintentos
                MaximoReintento = 5         ;//Antes 5 DCF reintentos 

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Puerto utilizado por el autorizador para imprimir mensajes de error
                String PuertoAImprimir;

                //Se inicializa el vector de control de fallo de comunicaci�n
                FalloComunicacion[0] = false;
                FalloComunicacion[1] = false;

                //Arma la trama de Transmision
                ArmarTramaTx(ComandoaEnviar);

                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {

                    if (EsTCPIP)
                        EnviarComando_TCPIP();

                    else
                         EnviarComando();

                    //Analiza la informaci�n recibida si se espera respuesta del Surtidor
                    if (BytesEsperados > 0)
                    {

                        if (EsTCPIP)
                            RecibirInformacion_TCPIP();
                        else
                            RecibirInformacion();

                        Reintentos += 1;
                    }
                } while (((FalloComunicacion[0] == true) || (FalloComunicacion[1] == true)) && (Reintentos < MaximoReintento));

                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion[0] == true || FalloComunicacion[1] == true)
                {
                    //EGV:Si la cara se va a Inactivar
                    if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                    {
                        PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                        EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                        EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                        IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa Inactivaci�n en Fallo de Comunicaci�n");
                        SWRegistro.Flush();
                    }

                    //EGV:Si la cara se va a activar
                    if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                    {
                        PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                        EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                        string Mensaje = "No se puede ejecutar activaci�n: Cara " + CaraID + " con fallo de comunicaci�n";
                        bool Imprime = true;
                        bool Terminal = false;
                        ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar activaci�n: Fallo de comunicaci�n");
                        SWRegistro.Flush();
                    }

                    //Env�a ERROR EN TOMA DE LECTURAS, si NO hay comunicaci�n con el surtidor
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {
                        string MensajeErrorLectura = "Error en Comunicaci�n con Surtidor";
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            bool EstadoTurno = false;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            bool EstadoTurno = true;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                            CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        //Se establece valor de la variable para que indique que ya fue reportado el error
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|P�rdida de comunicaci�n (" + ComandoaEnviar + ")");
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                    }

                    if (FalloComunicacion[1] && !EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|P�rdida de comunicaci�n (" + ComandoaEnviar + ")");
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = true;
                    }
                    if (!FalloComunicacion[1] && EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Se reestablece comunciaci�n con surtidor (" + ComandoaEnviar + ")");
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el par�metro FALSE si hubo error en la trama o en la comunicaci�n con el surtidor
                    return false;
                }
                else
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Se reestablece comunciaci�n con surtidor (" + ComandoaEnviar + ")");
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el par�metro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo ProcesoEnvioComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }






        //ARMA LA TRAMA A SER ENVIADA
        private void ArmarTramaTx(ComandoSurtidor ComandoTx)
        {
            try
            {
                //Asigna a la cara a encustar el comando que fue enviado
                ComandoCaras = ComandoTx;

                /* Configuracion por defecto de los valores de TimeOut, Trama y Bytes Esperados (Aplica completamente a los
                 * Comandos Estado y Enviar Datos */
                TramaTx = new byte[1];

                if (ComandoCaras != ComandoSurtidor.CambiarPrecio &&
                    ComandoCaras != ComandoSurtidor.PredeterminarVentaDinero && ComandoCaras != ComandoSurtidor.PredeterminarVentaVolumen)
                    TramaTx[0] = Convert.ToByte(Convert.ToByte(ComandoTx) | CaraEncuestada);

                BytesEsperados = 1;
                TimeOut = 200; //Antes 120
                // Configuracion de los valores de TimeOut, Trama y Bytes Esperados diferente a Comandos: Estado y Enviar Datos
                if ((ComandoTx != ComandoSurtidor.Estado) && (ComandoTx != ComandoSurtidor.EnviarDatos))
                {
                    switch (ComandoTx)
                    {
                        case (ComandoSurtidor.Autorizar):           //Autoriza Despacho
                        case (ComandoSurtidor.Detener):             //Detiene Despacho
                            TimeOut = 80;//Antes 50
                            BytesEsperados = 0;
                            break;

                        case (ComandoSurtidor.TotalDespacho):       //Pide datos de Final de Despacho
                            TimeOut = 600;
                            BytesEsperados = 33;
                            break;

                        case (ComandoSurtidor.Totales):             //Pide Totalizadores
                            TimeOut = 1000; //800;
                            BytesEsperados = 94;
                            break;

                        case (ComandoSurtidor.ParcialDespacho):     //Pide Parical de Venta
                            TimeOut = 350;//30/05/2017
                            BytesEsperados = 6;
                            break;

                        case (ComandoSurtidor.DetenerTodos):        //Detiene todos los despachos
                            TimeOut = 80;//Antes 50
                            TramaTx[0] = Convert.ToByte(ComandoTx);
                            BytesEsperados = 0;
                            break;

                        case (ComandoSurtidor.CambiarPrecio):       //Cambio de Precio


                            //Borrar 

                            SWRegistro.WriteLine(DateTime.Now + "|EstructuraRedSurtidor[CaraEncuestada].FactorPreci = |" + EstructuraRedSurtidor[CaraEncuestada].FactorPrecio +
                                " |EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 =  " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1);
                            SWRegistro.Flush();


                            //Se coloca los Nibble que almacena los bytes en 0x0(TramaTx[6]-TramaTx[9]) 
                            TramaTx = new byte[13] { 0xFF, 0xE5, 0xF4, 0xF6, 0xE0, 0xF7, 0xE0, 0xE0, 0xE0, 0xE0, 0xFB, 0xE0, 0xF0 };
                            //Se almacena en una variable String el valor a ser enviado en la trama
                            string strPrecio = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 *
                                EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)).PadLeft(4, '0');
                            //Se completa la trama con el precio a partir de la variable String
                            for (int i = 0; i <= 3; i++)
                                TramaTx[i + 6] = Convert.ToByte((Convert.ToByte(strPrecio.Substring(3 - i, 1))) | TramaTx[i + 6]);
                            TramaTx[11] = Convert.ToByte(TramaTx[11] | CalcularLRC(TramaTx, 0, 10));
                            TimeOut = 80;//Antes 50
                            BytesEsperados = 0;
                            break;

                        //27/03/2013 Predeterminacion por IMporte 
                        case (ComandoSurtidor.PredeterminarVentaDinero): //Predetermina una venta con un valor especifico de Dinero 
                            //Se coloca los Nibble que almacena los bytes de Preset en 0x00(TramaTx[6]-TramaTx[9]) 
                            TramaTx = new byte[12] { 0xFF, 0xE6, 0xF2, 0xF8, 0xE0, 0xE0, 0xE0, 0xE0, 0xE0, 0xFB, 0xE0, 0xF0 };
                            string ValoraPredeterminarDinero =
                                Convert.ToString(Convert.ToInt64(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado)).PadLeft(5, '0'); //100 para Cartagena
                            for (int i = 4; i <= 8; i++)
                                TramaTx[i] = Convert.ToByte((Convert.ToByte(ValoraPredeterminarDinero.Substring(8 - i, 1))) | TramaTx[i]);
                            TramaTx[10] = Convert.ToByte(TramaTx[10] | CalcularLRC(TramaTx, 0, 9));
                            TimeOut = 80;//Antes 50
                            BytesEsperados = 0;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Valor de Importe enviado a Surtidor: " + ValoraPredeterminarDinero);
                            SWRegistro.Flush();
                            break;

                        case (ComandoSurtidor.PredeterminarVentaVolumen): //Predetermina una venta con un valor especifico de Metros cubicos
                            //Se coloca los Nibble que almacena los bytes de Preset en 0x00(TramaTx[6]-TramaTx[9]) 
                            TramaTx = new byte[15] { 0xFF, 0xE3, 0xF1, 0xF4, 0xF6, 0xE0, 0xF8, 0xE0, 0xE0, 0xE0, 0xE0, 0xE0, 0xFB, 0xE0, 0xF0 };
                            string ValoraPredeterminarVolumen =
                                Convert.ToString(Convert.ToInt64(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado)).PadLeft(5, '0');
                            for (int i = 7; i <= 11; i++)
                                TramaTx[i] = Convert.ToByte((Convert.ToByte(ValoraPredeterminarVolumen.Substring(11 - i, 1))) | TramaTx[i]);
                            TramaTx[13] = Convert.ToByte(TramaTx[13] | CalcularLRC(TramaTx, 0, 12));
                            TimeOut = 80; //Antes 50
                            BytesEsperados = 0;


                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Valor de Volumen enviado a Surtidor: " + ValoraPredeterminarVolumen);
                            SWRegistro.Flush();


                            break;
                    }
                }
                //Almacena la cantidad de byte eco, que vendr�a en la trama de respuesta
                eco = Convert.ToByte(TramaTx.Length);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo ArmarTramaTx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
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

                ///////////////////////////////////////////////////////////////////////////////
                //LOGUEO DE TRAMA TRANSMITIDA
                string strTrama = "";
                for (int i = 0; i <= TramaTx.Length - 1; i++)
                    strTrama += TramaTx[i].ToString("X2") + " ";

                //SWTramas.WriteLine(DateTime.Now + " Tx.Cara " + CaraID + ". " + strTrama);
              


                SWTramas.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                        DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') + "|" + CaraID + "|Tx|" + strTrama);

                SWTramas.Flush();
                /////////////////////////////////////////////////////////////////////////////////

                //Tiempo muerto mientras el Surtidor Responde
                //Thread.Sleep(TimeOut+ 500); //DCF solo para pruebas remotas 
                Thread.Sleep(TimeOut);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo EnviarComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
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
                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + Bytes_leidos.ToString());
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

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + Bytes_leidos.ToString());
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

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + Bytes_leidos.ToString());
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
                //Thread.Sleep(150);
                //VerificarConexion();
                //Stream.Write(TramaTx, 0, TramaTx.Length);
                //Stream.Flush();
            }
        }

        public static void CallBackMethod(IAsyncResult asyncresult)
        {

        }

        public void VerificarConexion()
        {
            int iReintento = 0;
            string Comando = "";
            try
            {
                if (ClienteGilbarco == null)
                {
                    Boolean EsInicializado = false;
                    SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*7|Verificando conexion 1 " + EsInicializado);

                    SWTramas.Flush();
                    while (!EsInicializado)
                    {
                        try
                        {
                            SWTramas.WriteLine(
                            DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                            DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                            DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                            DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                            "|" + CaraID + "|*8|Verificando conexion 2 " + EsInicializado);

                            SWTramas.Flush();
                            ClienteGilbarco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                            SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*9|Verificando conexion 3" + EsInicializado);

                            SWTramas.Flush();

                            if (ClienteGilbarco == null)
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

                        if (ClienteGilbarco != null)
                        {
                            //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Inicializada - Ip: " + DireccionIP + " Puerto: " + Puerto);
                            //SWRegistro.Flush();
                            EsInicializado = true;
                        }
                    }
                    SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*9|Verificando conexio 4");

                    SWTramas.Flush();
                }

                Boolean estadoAnterior = true;
                if (!this.ClienteGilbarco.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClienteGilbarco.Client.BeginDisconnect(true, callBack, ClienteGilbarco);

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



                while (!this.ClienteGilbarco.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteGilbarco.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteGilbarco);
                        //ClienteGilbarco.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteGilbarco.Client.Connected)
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
                this.Stream = ClienteGilbarco.GetStream();
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
                ClienteGilbarco.Close();
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
                ClienteGilbarco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteGilbarco.GetStream();
                if (this.ClienteGilbarco.Client.Connected == true)
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

        public void LimpiarSockets()
        {
            try
            {
                //ClienteGilbarco.Client.Disconnect(false);  
                ClienteGilbarco.Client.Close();
                ClienteGilbarco.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteGilbarco = null;
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|LimpiarSockets:" + ex.Message);
                SWRegistro.Flush();

            }

        }


        public void VerifySizeFile()
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-GilbarcoKraus-Tramas(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                FileInf = new FileInfo(ArchivoRegistroSucesos);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + " GilbarcoKraus-RegistroSucesos (" + Puerto + ").txt";
                    SWRegistro = File.AppendText(ArchivoRegistroSucesos);
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|VerifySizeFile: " + Excepcion);
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
                BytesEsperados = BytesEsperados + eco;


                if (Stream == null)
                {
                    ErrorComunicacion = true;
                    return;
                }

                if (!Stream.DataAvailable)
                    Thread.Sleep(40);



                if (Stream.DataAvailable)
                {

                    //DCF Modificacion 14/08/2015 EDS Sodis Aeropuerto  Leer siempre la respuesta del surtidor
                    byte[] TramaRxTemporal = new byte[1024]; // para que tome toda lo escrito en el buffer problemas con los byte esperado cuando se tiene mas grado configurado dentro del surtidor 



                    // Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                    if (Stream.CanRead)
                    {
                        do
                        {
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                        } while (Stream.DataAvailable);
                    }

                    //DCF Modificacion 14/08/2015 EDS Sodis Aeropuerto  Leer siempre la respuesta del surtidor


                    //LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                    ErrorComunicacion = false;


                    //Definicion de Trama Temporal
                    byte[] TramaTemporal = new byte[Bytes_leidos];

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
                        "|" + CaraID + "|Rx|" + strTrama + "| #| " + TramaRx.Length);

                    SWTramas.Flush();

                   

                    //Permite loguear todo  lo que llegue como RX 25/06/2016
                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    if (Bytes_leidos == BytesEsperados || (ComandoCaras == ComandoSurtidor.Totales)) // que se lea siempre la respuesta del surtidor  al final se analiza y se indica si existe error 
                    {
                        /////////////////////////////////////////////////////////////////////////////////

                        AnalizarTrama();

                    }

                      //DCF Modificacion 14/08/2015 EDS Sodis Aeropuerto  Leer siempre la respuesta del surtidor 
                    else if (ErrorComunicacion == false)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Error|" + " Bytes_leidos = " + Bytes_leidos + " | BytesEsperados = |" + BytesEsperados);
                        SWRegistro.Flush();

                        ErrorComunicacion = true;                       

                    }

                    Thread.Sleep(20);//para pausa luego de cada RX y no solapar el proximo TX  DCF reintentos 

                    //SWRegistro.Flush();
                }
                else if (ErrorComunicacion == false)
                {

                    ErrorComunicacion = true;

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


        //LEE Y ALMACENA LA TRAMA RECIBIDA
        private void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;

                if (!TramaEco)
                    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                BytesEsperados = BytesEsperados + eco;

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
              
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

                    ///////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA RECIBIDA
                    string strTrama = "";
                    for (int i = 0; i <= TramaRx.Length - 1; i++)
                        strTrama += TramaRx[i].ToString("X2") + " ";

                  //  SWTramas.WriteLine(DateTime.Now + " Rx.Cara " + CaraID + ". " + strTrama);

                    SWTramas.WriteLine(
                        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                            DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                            DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                            DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +"|" + CaraID + "|Rx|" + strTrama);

                    SWTramas.Flush();
                    /////////////////////////////////////////////////////////////////////////////////

                if (Bytes == BytesEsperados || Bytes == 35 || Bytes == 65 || Bytes == 95 || Bytes == 125 || Bytes == 155 || Bytes == 185
                     || Bytes == 47 || Bytes == 89 || Bytes == 131 || Bytes == 172 || Bytes == 215 || Bytes == 257)//para No cambiar en el configurador los surtidores que tiene 3 manguera pero solo despachan por 1 Para varias Mangueras 23-06-2015                       
                    {
                        AnalizarTrama();
                    }
                else if (FalloComunicacion[1] == false)
                    FalloComunicacion[1] = true;

                SWRegistro.Flush();

                Thread.Sleep(20);//Tiempo de espera generar erro en lo glp 30/05/2017


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void AnalizarTrama()
        {
            try
            {
                switch (ComandoCaras)
                {
                    case (ComandoSurtidor.Estado):
                        AsignarEstado();
                        break;
                    case (ComandoSurtidor.TotalDespacho):
                        RecuperarDatosFindeVenta();
                        break;
                    case (ComandoSurtidor.Totales):
                        RecuperarTotalizadores();
                        break;
                    case (ComandoSurtidor.ParcialDespacho):
                        RecuperarParcialesdeVenta();
                        break;
                    case (ComandoSurtidor.EnviarDatos):
                        ConfirmacionEnvioDatos();
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo AnalizarTrama: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void AsignarEstado()
        {
            try
            {
                //Se separan el Codigo del estado y la cara en variables diferentes.  La "e" es el parametro aditivo del ECO recibido
                byte CodigoEstado = Convert.ToByte(TramaRx[0] & (0xF0));

                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;

                byte CaraqueResponde = Convert.ToByte(TramaRx[0] & (0x0F));
                //Evalua si la informacion que se recibio como respuesta corresponde a la cara que fue encuestada
                if (CaraqueResponde == CaraEncuestada)
                {
                    FalloComunicacion[0] = false; //No hubo error por fallas en datos
                    //Asigna Estado
                    switch (CodigoEstado)
                    {
                        case (0x00):
                            //EGV:Lo descoment� porque en colombia esta asi
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Error;
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Estado ERROR");
                            SWRegistro.Flush();
                            break;
                        case (0x60):
                            if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Finaliza venta en Estado Espera");
                                SWRegistro.Flush();
                            }
                            else
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Espera;
                            break;
                        case (0x70):
                            /* CASO ESPECIAL KRAUS: En caso que el surtidor se detenga y cambie su estado a Requiere Autorizacion*/
                            //----------------------------------------------------------------------------------------------------------------------//
                            //EGV:Coment� esto porque en colombia esta as�
                            //if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho)
                            //    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorReautorizar;
                            if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho && EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.PorReautorizar)
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorReautorizar;
                            //------------------------------------------------------------------------------------------------------------------------//
                            else
                            {
                                //EGV:Coment� esto porque en colombia esta as�
                                //if (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Espera &&
                                //    EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.FinDespachoA &&
                                //    EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.FinDespachoB &&
                                //    EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.PorAutorizar)
                                //{
                                //    SWRegistro.WriteLine(DateTime.Now + " Cara " + CaraEncuestada + ". Pide autorizaci�n con estado anterior: " +
                                //        EstructuraRedSurtidor[CaraEncuestada].Estado);
                                //    SWRegistro.Flush();
                                //}
                                //EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorAutorizar;

                                if (!EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                                    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorAutorizar;
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Forzando Estado Fin de Venta en Estado PorAutorizar");
                                    SWRegistro.Flush();
                                    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                                }
                            }
                            break;
                        case (0x80):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Autorizado;
                            break;
                        case (0x90):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;
                            break;
                        case (0xA0):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoA;
                            break;
                        case (0xB0):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoB;
                            break;
                        case (0xC0):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Detenido;
                            break;
                        case (0xD0):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.EsperandoDatos;
                            break;
                        default:
                            //EstadoCaras[CaraEncuestada - 1] = EstadoCara.Indeterminado;
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|EstadoIndeterminado: " + CodigoEstado +
                            " - Comando enviado: " + ComandoCaras);
                            SWRegistro.Flush();
                            FalloComunicacion[0] = true;
                            break;
                    }
                    //Almacena en archivo el estado actual del surtidor
                    if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|" + EstructuraRedSurtidor[CaraEncuestada].Estado);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Cara que Responde: " + CaraqueResponde + " (" + ComandoCaras + ")");
                    SWRegistro.Flush();
                    FalloComunicacion[0] = true;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo AsignarEstado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        private void TomarAccion()
        {
            try
            {
                int Reintentos = 0;
                //Puerto utilizado por el autorizador para imprimir mensajes de error
                String PuertoAImprimir;

                //Solamente ingresa a esta parte de c�digo cuando no se ha inicializado la cara (inicio de programa)
                if (EstructuraRedSurtidor[CaraEncuestada].CaraInicializada == false)
                {
                    //Si la cara esta en reposo
                    if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera || EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.PorAutorizar)
                    {
                        //Realiza cambio de PRECIO BASE
                        //CambiarPrecio();

                        //Cambia bandera inidicando que la cara fue inicializada correctamente
                        EstructuraRedSurtidor[CaraEncuestada].CaraInicializada = true;
                    }
                }

                //Realiza la respectiva tarea en la normal ejecuci�n del proceso
                switch (EstructuraRedSurtidor[CaraEncuestada].Estado)
                {
                    /***************************ESTADO EN ESPERA***************************/
                    case (EstadoCara.Espera):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa Inactivaci�n en Estado Espera");
                            SWRegistro.Flush();

                            //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante inactivaci�n
                            if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                            {
                                string MensajeErrorLectura = "Cara Inactivada";
                                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = false;
                                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }

                                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = true;
                                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                            }

                            //Sale del Caso si se inactiva
                            break;
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            ArrayLecturas = TomarLecturaActivacionCara();
                            if (ArrayLecturas.Count > 0)
                            {
                                //Instancia Array para reportar las lecturas
                                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                                ArrayLecturas.CopyTo(LecturasEnvio);
                                //Lanza Evento para reportar las lecturas despu�s de un cambio de tarjeta
                                LecturasCambioTarjeta(LecturasEnvio);
                                //Inicializa bandera que indica la activaci�n de una cara
                                EstructuraRedSurtidor[CaraEncuestada].ActivarCara = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa Activaci�n en Estado Espera.");
                                SWRegistro.Flush();

                                //EGV: Mando a cambiar los precios de la cara
                                CambiarPrecio();
                            }
                        }

                        //Informa cambio de estado
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            int mangueraColgada = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD;
                            CaraEnReposo(CaraID, mangueraColgada);

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "| Manguera: " + mangueraColgada + " - Evento: Informa cara en Espera");
                            SWRegistro.Flush();

                            //Si hab�a venta por predeterminar, al colgar la manguera el sistema cancela el proceso de predeterminado
                            EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado = 0;

                            //Si hab�a venta por predeterminar, al colgar la manguera el sistema cancela el proceso de predeterminado
                            if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen)
                                EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen = false;
                            if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                                EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte = false;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            LecturaAperturaCierre();

                        //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                        /*if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioSurtidorNivel1 !=
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1)
                            CambiarPrecio();*/
                        break;

                    /***************************ESTADO EN DESPACHO***************************/
                    case (EstadoCara.Despacho):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            string Mensaje = "No se puede ejecutar inactivaci�n: Cara " + CaraID + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Inactivaci�n: Cara en despacho");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activaci�n: Cara " + CaraID + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Activaci�n: Cara en despacho");
                            SWRegistro.Flush();
                            break;
                        }

                        //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho                    
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;


                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;

                        //Pedir Parciales de Venta SE QUITA PARA SOLO PRUEBAS CA�ATE ojo DESCOMENTAR
                        ArmarTramaTx(ComandoSurtidor.ParcialDespacho);//30/05/2017


                        if (EsTCPIP)
                        {
                            EnviarComando_TCPIP();
                            RecibirInformacion_TCPIP();
                        }

                        else
                        {
                            EnviarComando();
                            RecibirInformacion();
                        }




                        //Dispara evento al programa principal si no hubo fallo en la recepcion de los datos                    
                        //if (FalloComunicacion[1] == false)
                        if (FalloComunicacion[1] == false && VentaParcial != null)//JD 24/08/2015
                        {
                            string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                            string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                            VentaParcial(CaraID, strTotalVenta, strVolumen);
                        }
                        break;

                    /***************************ESTADO DETENIDO***************************/
                    case (EstadoCara.Detenido):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa Inactivaci�n en Estado Detenido");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activaci�n: Cara " + CaraID + " en estado Detenido";
                            bool Imprime = true;
                            bool Terminal = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar activaci�n: Cara en estado Detenido");
                            SWRegistro.Flush();
                            break;
                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|En estado Detenido");
                            SWRegistro.Flush();
                        }

                        //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho   
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara Detenida";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                        break;

                    /***************************ESTADO FIN DESPACHO A***************************/
                    case (EstadoCara.FinDespachoA):
                    /***************************ESTADO FIN DESPACHO B***************************/
                    case (EstadoCara.FinDespachoB):
                    /***************************ESTADO FIN DESPACHO FORZADO***************************/
                    case (EstadoCara.FinDespachoForzado):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            string Mensaje = "No se puede ejecutar inactivaci�n: Cara " + CaraID + " en Fin de Venta";
                            bool Imprime = true;
                            bool Terminal = false;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Inactivaci�n: Cara en estado Fin de Venta");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activaci�n: Cara " + CaraID + " en Fin de Despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Activaci�n: Cara en estado Fin de Venta");
                            SWRegistro.Flush();
                            break;
                        }

                        //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho                    
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en Fin de Despacho";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Si la venta no ha sido finalizada, se ejecuta proceso para finalizarla
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                            ProcesoFindeVenta();
                        break;

                    /***************************ESTADO DE ERROR***************************/
                    case (EstadoCara.Error):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa Inactivaci�n en Estado de Error");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activaci�n: Cara " + CaraID + " en estado de Error";
                            bool Imprime = true;
                            bool Terminal = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar activaci�n: Cara en estado de Error");
                            SWRegistro.Flush();
                            break;
                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|En estado de Error");
                            SWRegistro.Flush();
                        }

                        //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara est� en Estado de Error
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en estado de ERROR";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Iniciales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                        break;

                    /***************************ESTADO POR AUTORIZAR***************************/
                    case (EstadoCara.PorAutorizar):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            string Mensaje = "No se puede ejecutar inactivaci�n: Cara " + CaraID + " en intento de autorizaci�n";
                            bool Imprime = true;
                            bool Terminal = false;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Inactivaci�n: Cara en estado Por Autorizar");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activaci�n: Cara " + CaraID + " en estado Por Autorizar";
                            bool Imprime = true;
                            bool Terminal = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Activaci�n: Cara en estado Por Autorizar");
                            SWRegistro.Flush();
                            break;
                        }

                        //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara est� en Estado de Error
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Manguera descolgada";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Iniciales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Informa cambio de estado s�lo si la venta anterior ya fue liquidada
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado &&
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                        {
                            TomarLecturas(); //Modificado 2011.10.08 - 0830"

                            int IdProducto =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].IdProducto;
                            int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD;
                            string Lectura = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");
                            AutorizacionRequerida(CaraID, IdProducto, IdManguera, Lectura,"");
                        }
                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                        {
                            //Obtiene la Lectura Inicial de la Venta
                            // TomarLecturas(); //DCF Modificado 2011.10.08 - 0830"

                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura > 0)
                                //EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta 
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta =
                               EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura;
                            else
                            {

                                //EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta =
                                //EstructuraRedSurtidor[CaraEncuestada].LecturaFinalVenta;
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta =
                               EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta;
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Lectura inicial en 0, asume Lectura Final");
                                SWRegistro.Flush();
                            }

                            //string strLecturasVolumen = EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta.ToString("N3");
                            string strLecturasVolumen = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta.ToString("N3");

                            LecturaInicialVenta(CaraID, strLecturasVolumen);

                            //Loguea Evento de envio de lectura
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento InformarLecturaInicialVenta. Lectura Inicial: " +
                                strLecturasVolumen);
                            SWRegistro.Flush();

                            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioSurtidorNivel1;

                            //Si la siguiente venta es predeterminada, realiza el proceso de programaci�n
                            if (EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado > 0)
                                Predeterminar();

                            //Env�a comando de Autorizaci�n
                            Reintentos = 0;
                            do
                            {
                                ProcesoEnvioComando(ComandoSurtidor.Autorizar);
                                Thread.Sleep(50); // DCF reintentos
                                if (EsTCPIP) // DCF reintentos
                                    RecibirInformacion_TCPIP();    // DCF reintentos                            
                                
                                Reintentos++;
                                Thread.Sleep(30);
                                ProcesoEnvioComando(ComandoSurtidor.Estado);
                            } while (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizado &&
                                EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho && (Reintentos <= 3));

                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizado ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                            }
                        }

                        break;

                    /***************************ESTADO POR REAUTORIZAR***************************/
                    case (EstadoCara.PorReautorizar):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            string Mensaje = "No se puede ejecutar inactivaci�n: Cara " + CaraID + " en intento de Reautorizaci�n";
                            bool Imprime = true;
                            bool Terminal = false;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Inactivaci�n: Cara en Estado de Reautorizaci�n");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activaci�n: Cara " + CaraID + " en estado Por Reautorizar";
                            bool Imprime = true;
                            bool Terminal = false;
                            ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|No se puede ejecutar Activaci�n: Cara en Estado de Reautorizaci�n");
                            SWRegistro.Flush();
                            break;
                        }

                        //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara est� en Estado de Reautorizaci�n
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en Despacho/Reautorizaci�n";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Coment� esto porque en colombia no esta asi esto
                        //Obtiene la Lectura Inicial de la Venta
                        //TomarLecturas();
                        //if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura > 0)
                        //{
                        //    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura == EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta &&
                        //        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                        //    {
                        //        Reintentos = 0;
                        //        do
                        //        {
                        //            ProcesoEnvioComando(ComandoSurtidor.Autorizar);
                        //            Reintentos++;
                        //            Thread.Sleep(30);
                        //            ProcesoEnvioComando(ComandoSurtidor.Estado);
                        //        } while (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizado &&
                        //        EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho && (Reintentos <= 3));

                        //        EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                        //        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;

                        //        SWRegistro.WriteLine(DateTime.Now + " Cara " + CaraEncuestada + ". Reautorizando. Lectura tomada: " +
                        //            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura +
                        //            " - Reintentos: " + Reintentos);
                        //        SWRegistro.Flush();
                        //    }
                        //    else
                        //    {
                        //        SWRegistro.WriteLine(DateTime.Now + " Cara " + CaraEncuestada + ". Lectura Inicial de venta (" +
                        //            EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta + ") diferente a Lectura tomada en reautorizaci�n (" +
                        //            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura + ") - Venta Parcial: " + EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial);
                        //        SWRegistro.Flush();
                        //    }

                        //}
                        //else
                        //{
                        //    SWRegistro.WriteLine(DateTime.Now + " Cara " + CaraEncuestada + ". Lectura tomada en 0 (Reautorizando)");
                        //    SWRegistro.Flush();
                        //}

                        //Reconfirma estado
                        Thread.Sleep(20);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Inicia reconfirmacion de Estado");
                        SWRegistro.Flush();
                        if (ProcesoEnvioComando(ComandoSurtidor.Estado))
                        {
                            if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.PorReautorizar)
                            {
                                //Obtiene la Lectura Inicial de la Venta
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Inicia Toma de Lectura en mitad de venta");
                                SWRegistro.Flush();
                                TomarLecturas();
                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura > 0)
                                {
                                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura == EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta
                                        && EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Inicia proceso de Reautorizacion");
                                        SWRegistro.Flush();
                                        Reintentos = 0;
                                        do
                                        {
                                            ProcesoEnvioComando(ComandoSurtidor.Autorizar);
                                            Reintentos++;
                                            Thread.Sleep(30);
                                            ProcesoEnvioComando(ComandoSurtidor.Estado);

                                            if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoA ||
                                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoB ||
                                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado ||
                                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera)
                                            {
                                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID +
                                                    "|Proceso|Cancela Reautorizacio. Envia Detencion de venta");
                                                SWRegistro.Flush();
                                                ProcesoEnvioComando(ComandoSurtidor.Detener);
                                                break;
                                            }
                                        } while ((EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizado) &&
                                            (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho) && (Reintentos <= 3));

                                        if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizado ||
                                            EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho)
                                        {
                                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;

                                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Venta reautorizada. Lectura: " +
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura + " - Reintentos: " + Reintentos);
                                            SWRegistro.Flush();
                                        }
                                        else if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoA ||
                                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoB ||
                                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado ||
                                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera)
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Inicia proceso de Fin de Venta despu�s de Reautorizado");
                                            SWRegistro.Flush();
                                            ProcesoFindeVenta();
                                        }
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Lectura Inicial de venta (" +
                                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta + ") diferente a Lectura tomada en reautorizaci�n (" +
                                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura + ")");
                                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Se asume que es una nueva venta. Inicia Proceso Fin de Venta");
                                        SWRegistro.Flush();
                                        //ProcesoFindeVenta(); // 2012.03.02 - 1634.. DCF  //No enviar a ProcesoFindeVenta, se realiza el fin de venta cuando se cuelgue la manguera ******
                                    }
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Error|Lectura 0 en Reautorizando");
                                    SWRegistro.Flush();
                                }
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID +
                                    "|Proceso|Estado an�malo PorReautorizar. Estado Verdadero: " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                                SWRegistro.Flush();
                                if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoA ||
                                    EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoB ||
                                    EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado ||
                                    EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Proceso|Inicia proceso de Fin de Venta despu�s de confirmaci�n de Estado PorReautorizar");
                                    SWRegistro.Flush();
                                    ProcesoFindeVenta();
                                }
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID +
                                    "|Error|No respondio Comando Estado para confirmar Estado PorReautorizar");
                            SWRegistro.Flush();
                        }

                        break;

                    /***************************ESTADO INDETERMINADO***************************/
                    default:
                        //Env�a ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara est� en Estado de Reautorizaci�n                    
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara no colgada (estado indeterminado)";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo TomarAccion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();

            }
        }

        //REALIZA PROCESO DE FIN DE VENTA
        private void ProcesoFindeVenta()
        {
            try
            {
                //Inicializacion de variables
                EstructuraRedSurtidor[CaraEncuestada].Volumen = 0;
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = 0;

                //int Reintentos = 0;
                decimal VolumenCalculado = new decimal();


                //Obtiene los Valores Finales de la Venta (Precios y Metros cubicos despachados)
                if (ProcesoEnvioComando(ComandoSurtidor.TotalDespacho))
                {
                    //Obtiene la Lectura Final de la Venta
                    TomarLecturas();
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura;

                    //Calcula el volumen despachado seg�n lecturas Inicial y Final de venta
                    //VolumenCalculado = EstructuraRedSurtidor[CaraEncuestada].LecturaFinalVenta - EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta;
                    VolumenCalculado = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta - EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta;
                    if (VolumenCalculado == 0)  //Modificado 28-04-2012 - 1208 -- log Lectura Inicial = Lectura Final
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Lectura Inicial = Lectura Final = " +
                         EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta);
                        SWRegistro.Flush();

                        EstructuraRedSurtidor[CaraEncuestada].Volumen = 0;
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta = 0;// si no se mueven los totalizadore los datos de venta son CERO //02-05-2012-1000
                    }

                    //Realiza comparaci�n entre volumen calculado por lecturas y volumen obtenido por finalizaci�n de venta
                    // Tiene en cuenta si se reiniciaron las lecturas por secuencia normal del Totalizador del surtidor
                    if (VolumenCalculado > 0)
                    {
                        //Si no se ha reiniciado el sistema, el valor de LecturaInicial es diferente de 0
                        //if (EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta > 0)
                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta > 0)
                        {
                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta > 0)
                            {
                                /*Se compara el valor de Volumen Calculado con el valor de Volumen Recibido.
                                 * La diferencia no debe exceder el (+/-) 1%.  
                                 * Se da mayor credibilidad al calculado por lecturas*/

                                if (EstructuraRedSurtidor[CaraEncuestada].Volumen < VolumenCalculado - Convert.ToDecimal(0.05) ||
                                    EstructuraRedSurtidor[CaraEncuestada].Volumen > VolumenCalculado + Convert.ToDecimal(0.05))
                                {

                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Volumen Calculado: " + VolumenCalculado +
                                        " - Volumen Reportado: " + EstructuraRedSurtidor[CaraEncuestada].Volumen +
                                        "|Importe Calculado: " + VolumenCalculado * EstructuraRedSurtidor[CaraEncuestada].PrecioVenta +
                                        " - Importe Reportado: " + EstructuraRedSurtidor[CaraEncuestada].TotalVenta);
                                    SWRegistro.Flush();

                                    //Importe  y Volumen Calculado DCF 03/03/2012
                                    EstructuraRedSurtidor[CaraEncuestada].Volumen = VolumenCalculado;
                                    EstructuraRedSurtidor[CaraEncuestada].TotalVenta = VolumenCalculado * EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;// 
                                }
                            }
                            else
                            {
                                //EstructuraRedSurtidor[CaraEncuestada].LecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta + EstructuraRedSurtidor[CaraEncuestada].Volumen;

                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta + EstructuraRedSurtidor[CaraEncuestada].Volumen;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Lectura Final de Venta en 0. Calculada: " +
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta);
                                SWRegistro.Flush();
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Lectura Inicial en 0");
                            SWRegistro.Flush();
                        }
                    }

                    //Si se realiz� una venta con valores de m3 y $ mayor que cero
                    if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0)
                    {
                        if (EstructuraRedSurtidor[CaraEncuestada].TotalVenta == 0)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                                EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Total calculado " +
                                EstructuraRedSurtidor[CaraEncuestada].TotalVenta);
                            SWRegistro.Flush();
                        }

                        //Dispara evento al programa principal si la venta es diferente de 0
                        string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                        string strPrecio = EstructuraRedSurtidor[CaraEncuestada].PrecioVenta.ToString("N3");
                        string strLecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaFinalVenta.ToString("N3");
                        string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                        string strLecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].LecturaInicialVenta.ToString("N3"); //EstructuraRedSurtidor[CaraEncuestada].LecturaInicialVenta.ToString("N3");
                        byte bytProducto = Convert.ToByte(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].IdProducto);
                        int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD;
                        String PresionLlenado = "0";

                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;
                        VentaFinalizada(CaraID, strTotalVenta, strPrecio, strLecturaFinalVenta,
                                 strVolumen, Convert.ToString(bytProducto), IdManguera, PresionLlenado, strLecturaInicialVenta);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa fin de venta: Importe: " + strTotalVenta +
                            " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta +
                            " - Lectura Final: " + strLecturaFinalVenta + " - Volumen: " + strVolumen + " - Presi�n: " + PresionLlenado);
                        SWRegistro.Flush();
                    }
                    else
                    {
                        VentaInterrumpidaEnCero(CaraID);
                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Venta en CERO");
                        SWRegistro.Flush();
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo ProcesoFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //OBTIENE LOS VALORES FINALES DE UNA VENTA
        private void RecuperarDatosFindeVenta()
        {
            try
            {
                //Calcula el LRC
                int LRCCalculado = CalcularLRC(TramaRx, 0, (TramaRx.Length - 3));
                int LRCObtenidoEnTama = TramaRx[(TramaRx.Length - 2)] & 0x0F;

                //Si el LRC Recibido (TramaRx[TramaRx.Length - 2] AND 0x0F) es igual al calculado
                if (LRCObtenidoEnTama == LRCCalculado)//Eco
                {
                    byte CaraqueResponde = Convert.ToByte((TramaRx[4] + 1) & (0x0F));
                    if (CaraqueResponde == CaraEncuestada)
                    {
                        //Se obtiene el Precio con que se realizo la venta
                        EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                            ObtenerValor(12, 15) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;

                        //Se obtiene el Volumen despachado
                        EstructuraRedSurtidor[CaraEncuestada].Volumen =
                            ObtenerValor(17, 22) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;

                        //Se obtiene el Dinero despachado
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta =
                            ObtenerValor(24, 29) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;

                        //No hubo error por fallas en datos
                        FalloComunicacion[0] = false;
                    }
                    else
                    {
                        FalloComunicacion[0] = true;
                        SWRegistro.WriteLine(DateTime.Now + "|Cara encuestada|" + EstructuraRedSurtidor[CaraEncuestada].Cara + "|(" + ComandoCaras +
                            ") - Cara que Responde: " + CaraqueResponde);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                   // Thread.Sleep(200);//para que se recupere el surtidor del fallo o erro en la comunicacion  DCF reintentos 


                    TimeOut = TimeOut + 200; // en caso de falla en el totalizador aumentar 200 ms m�s //5 DCF reintentos 

                    MaximoReintento = 7; 

                    FalloComunicacion[0] = true;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|(" + ComandoCaras +
                        ") responde LRC Errado" + " - Nuevo MaximoReintento = " + MaximoReintento);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo RecuperarDatosFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //PARA TOMAR LECTURAS DE APERTURA Y/O CIERRE DE TURNO
        private void LecturaAperturaCierre()
        {
            try
            {
                TomarLecturas();

                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                //Almacena las lecturas en la lista
                ArrayLecturas.Add(Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].MangueraBD) + "|" +
                    Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura));

                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                ArrayLecturas.CopyTo(LecturasEnvio);

                //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                {
                    LecturaTurnoCerrado(LecturasEnvio);
                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa Lectura Final Turno: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura.ToString());
                    SWRegistro.Flush();
                }

                //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    LecturaTurnoAbierto(LecturasEnvio);
                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|Informa Lectura Inicial Turno: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura.ToString());
                    SWRegistro.Flush();


                    //Borra solo para comprobara los precio DCF 18/08/2011

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|PrecioSurtidorNivel1 " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioSurtidorNivel1.ToString());
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Evento|PrecioNivel1 " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1.ToString());

                    SWRegistro.Flush();



                    //Si hay cambio de precio pendiente (precio base: PrecioNivel1), lo aplica
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioSurtidorNivel1 !=
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + " - CambiarPrecio ()");

                        SWRegistro.Flush();

                        CambiarPrecio();

                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ENVIA COMANDO DE TOMA DE LECTURAS Y LANZA ENVENTO PARA REPORTAR LECTURAS AL SERVICIO WINDOWS
        private void TomarLecturas()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura = 0;

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.Totales))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Comando: " + ComandoCaras +
                            ". Error en Toma de Lectura");

                        SWRegistro.Flush();
                    }
                } while ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura == 0) && Reintentos <= 3);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo TomarLecturas: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EGV:OBTIENE LOS VALORES DE LAS LECTURAS CUANDO LA CARA ESTA INACTIVA
        private System.Collections.ArrayList TomarLecturaActivacionCara()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                ArrayLecturas = new System.Collections.ArrayList();

                //Se resetea la lectura de cada grado de la cara
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura = 0;

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.Totales))
                    {
                        //Si el proceso no fue exitoso, la funci�n devuelve False
                        return new System.Collections.ArrayList();
                    }
                } while (Reintentos <= 3 && ExistenLecturasEnCero(CaraEncuestada));

                //Se verifica si existen lecturas en cero para cada grado
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura == 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Manguera|" + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD + "|Lectura recibida en CERO - ACTIVACION DE CARA");
                        SWRegistro.Flush();
                    }

                    ArrayLecturas.Add(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD + "|" + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura);
                }

                //Si el proceso de toma de lecturas fue exitoso, devuelve el arreglo de lecturas
                return ArrayLecturas;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo TomarLecturaActivacionCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return new System.Collections.ArrayList();
            }
        }

        //Verifica que existan lecturas en cero en una cara en particular
        private bool ExistenLecturasEnCero(byte Cara)
        {
            Boolean Existen = false;
            for (int i = 0; i <= EstructuraRedSurtidor[Cara].ListaGrados.Count - 1; i++)
            {
                if (EstructuraRedSurtidor[Cara].ListaGrados[i].Lectura == 0)
                {
                    Existen = true;
                }
            }

            return Existen;
        }

        //OBTIENE LOS VALORES DE LAS LECTURAS (TOTALIZADORES)
        private void RecuperarTotalizadores()
        {
            try
            {
                //Calcula el LRC
                int LRCCalculado = CalcularLRC(TramaRx, 0, (TramaRx.Length - 3));//Eco
                int LRCObtenidoEnTrama = TramaRx[(TramaRx.Length - 2)] & 0x0F;
                //Si el LRC Recibido (TramaRx[TramaRx.Length - 2] AND 0x0F) es igual al calculado
                if (LRCObtenidoEnTrama == LRCCalculado)//Eco
                {
                    //Obtiene todos los valores de precio y lecturas de la cara
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura = ObtenerValor(4, 11) / EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador;//Eco
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioSurtidorNivel1 = ObtenerValor(22, 25) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;//Eco
                    FalloComunicacion[0] = false;


                }
                else
                {


                  //  Thread.Sleep(200);//para que se recupere el surtidor del fallo o erro en la comunicacion  DCF reintentos 

                    TimeOut = TimeOut + 200; // en caso de falla en el totalizador aumentar 200 ms m�s //5 DCF reintentos 

                    MaximoReintento = 8; 

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|(" + ComandoCaras +
                     ") responde LRC Errado" + " - Nuevo MaximoReintento_ = " + MaximoReintento + " - TimeOut= " + TimeOut);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo RecuperarTotalizadores: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //OBTIENE EL VALOR EN PESOS DE LA VENTA EN CURSO Y CALCULA A PARTIR DE ESTE Y EL PRECIO EL VALOR DE VOLUMEN
        private void RecuperarParcialesdeVenta()
        {
            try
            {
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = ObtenerValor(0, 5) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;
                //Si se tiene precio de venta
                if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta != 0)
                    EstructuraRedSurtidor[CaraEncuestada].Volumen =
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta / EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;
                //Si no se tiene precio de venta, se toma por defecto el precio nivel 1 del grado que se asume est� despachando
                else
                    EstructuraRedSurtidor[CaraEncuestada].Volumen =
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta /
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1;

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo RecuperarParcialesdeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EVALUA SI LA CARA ENVIO CONFIRMACION PARA ENVIO DE DATOS
        private void ConfirmacionEnvioDatos()
        {
            try
            {
                //Almacena el Nibble Respuesta
                byte Respuesta = Convert.ToByte(TramaRx[0] & (0xF0));

                FalloComunicacion[0] = false;

                //Se evalua si el Surtidor esta preparado para recibir los datos
                if (Respuesta == 0xD0)
                {
                    if (Convert.ToByte(TramaRx[0] & (0x0F)) != CaraEncuestada)
                    {
                        FalloComunicacion[0] = true;
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|(" + ComandoCaras
                            + ") no corresponde a Cara que responde (" + Convert.ToByte(TramaRx[0] & (0x0F)) + ")");
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    FalloComunicacion[0] = true;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|(" + ComandoCaras +
                        ")  Respuesta recibida erronea: " + TramaRx[0]);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo Confirmaci�nEnvioDatos: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //CAMBIA EL PRECIO DE LA CARA
        private void CambiarPrecio()
        {
            try
            {

                //SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|CambiarPrecio --- "); //Borrar DCF 18/08/2011
                //SWRegistro.Flush();


                int Reintentos = 0;
                do
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos))
                    {
                        ArmarTramaTx(ComandoSurtidor.CambiarPrecio);
                        // EnviarComando(); //08/06/2017

                        if (EsTCPIP)
                        {
                            EnviarComando_TCPIP();                        
                        }

                        else
                        {
                            EnviarComando();                            
                        }


                        ProcesoEnvioComando(ComandoSurtidor.Totales);
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|No acept� comando Env�o de datos para cambio de precio");
                        SWRegistro.Flush();
                    }

                    Reintentos += 1;
                } while (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioSurtidorNivel1 !=
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 && (Reintentos <= 3));

                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioSurtidorNivel1 !=
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|No se pudo establecer nuevo precio");
                    SWRegistro.Flush();
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Precio establecido exitosamente");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo CambiarPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //REALIZA PROCESO PARA PREDETERMINAR UNA VENTA (POR METROS CUBICOS O POR DINERO)
        public void Predeterminar()
        {
            try
            {
                if (EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen == 0) // DCF 06/07/2012 para bolivia en caso de no configurar el parametro FactorPredeterminacionImporte
                    EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen = 1;

                if (EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte == 0) // DCF 06/07/2012 para bolivia en caso de no configurar el parametro FactorPredeterminacionImporte
                    EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte = 1;


                if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos))//11/03/2013
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte == false)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|FactorPredeterminacionVolumen = " + EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Valor de Volumen Predeterminado: " + EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush(); //para chile

                        EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado =
                           Convert.ToInt16(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado * EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen); // para chile *10 --- New 23/01/2011 . -->EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte =10; 


                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen);

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Valor de Volumen Envido al surtidor = " + EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);


                        SWRegistro.Flush(); //para chile


                    }
                    if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte == true)//11/03/2013
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|FactorPredeterminacionImporte = " + EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte);
                        //SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|FactorVolumen = " + EstructuraRedSurtidor[CaraEncuestada].FactorVolumen);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Valor de Importe Predeterminado: " + EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush(); //para chile

                        //////EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado =
                        //////    Convert.ToInt16(EstructuraRedSurtidor[CaraEncuestada].FactorVolumen *
                        //////    EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado /
                        //////    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1);
                        //////ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen);
                        //////SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|Valor de Volumen (Importe) Predeterminado: " + EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        //////SWRegistro.Flush();

                        // //13/03/2013 cambio en la predeterminacon por importe 
                        EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado =
                           Convert.ToInt32((EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado /
                           EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1) * EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte); // para chile *10 --- New 23/01/2011 . -->EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte =10; )
                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Valor Predeterminado de (Importe Calculado) enviado al Surtidor: " + EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush();



                        ////para probar predeterminacion por importe:
                        ////27/03/2013 Predeterminacion por IMporte 

                        //EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado =
                        //  Convert.ToInt32((EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado * EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte)); // para chile *10 --- New 23/01/2011 . -->EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte =10; )

                        //ArmarTramaTx(ComandoSurtidor.PredeterminarVentaDinero);
                        ////SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Valor Predeterminado, enviado al Surtidor: " + EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        ////SWRegistro.Flush();





                    }

                    //EnviarComando(); //08/06/2017
                    if (EsTCPIP)
                    {
                        EnviarComando_TCPIP();
                    }
                    else
                    {
                        EnviarComando();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraID + "|No acept� comando Env�o de datos para Predeterminar");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo Predeterminar: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        #endregion

        #region METODOS AUXILIARES

        //CALCULA EL CARACTER DE REDUNDANCIA CICLICA
        private int CalcularLRC(byte[] Trama, int Inicio, int Fin)
        {
            try
            {
                int LRC = new int();
                for (int i = Inicio; i <= Fin; i++)
                    LRC += (Trama[i] & 0x0F);
                LRC = ((LRC ^ 0x0F) + 1) & 0x0F;
                return LRC;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo CalcularLRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 0;
            }
        }

        private decimal ObtenerValor(int PosicionInicial, int PosicionFinal)
        {
            try
            {
                decimal Valor = new decimal();
                for (int i = PosicionInicial; i <= PosicionFinal; i++)
                    Valor += Convert.ToDecimal((TramaRx[i] & 0x0F) * Math.Pow(10, i - PosicionInicial));
                return Valor;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo ObtenerValor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 0;
            }
        }

        #endregion

        #region EVENTOS DE LA CLASE

        //SE EJECUTA CADA PERIODO DE TIEMPO
        //private void PollingTimerEvent(object source, ElapsedEventArgs e)
        private void CicloCara()
        {
            try
            {
                //Se detiene el timer para realizar el respectivo proceso de encuesta
                //PollingTimer.Stop();



                while (CondicionCiclo)
                {
                    VerifySizeFile(); //Verificaci�n del tama�o de la carpeta de logueo.

                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                    {

                        //Si la cara est� activa, realizar proceso de encuesta
                        if (ORedCaras.Activa == true)
                        {

                            CaraEncuestada = ORedCaras.Cara;

                            CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF Alias

                            //Para manejar la cara 16 Fecha: 29-12-2015
                            if (CaraEncuestada == 16)
                            {
                                CaraEncuestada = 0;
                            }


                            //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                            if (ProcesoEnvioComando(ComandoSurtidor.Estado))
                                TomarAccion();
                        }
                        Thread.Sleep(20);
                    }
                    //Luego de realizado el proceso se reactiva el Timer
                    //PollingTimer.Start();

                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo PollingTimerEvent: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
       public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
        // public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid)
          {
            try
            {
                byte CaraTmp;

                CaraTmp = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                        " - Tipo de Programacion: " + TipoProgramacion);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para despachar
                    EstructuraRedSurtidor[CaraTmp].AutorizarCara = true;

                    //Valor a programar 
                    EstructuraRedSurtidor[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    EstructuraRedSurtidor[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

                    EstructuraRedSurtidor[CaraTmp].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (EstructuraRedSurtidor[CaraTmp].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = true;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;


                            ////Valor a programar Cambio Para CHILE 05/10/2011, se multiplica por 100 por configuracion en el surtidor.
                            //EstructuraRedSurtidor[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado) * EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen;// 100; //  Modificado 2011.10.05 - 1731

                            //EstructuraRedSurtidor[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado) * EstructuraRedSurtidor[CaraTmp].FactorVolumen * EstructuraRedSurtidor[CaraTmp].FactorPredeterminacionVolumen;// * EstructuraRedSurtidor[CaraEncuestada].FactorVolumen DCF 09/07/2012


                            // EstructuraRedSurtidor[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado); //

                        }
                        else
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = true;
                        }
                    }
                    else
                    {
                        EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false;
                        EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo oEvento_VentaAutorizada: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
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
                    //Objeto Producto para a�adir al Diccionario
                    Producto PrecioProducto = new Producto();

                    string[] vPreciosProducto = sPreciosProducto.Split('|');
                    PrecioProducto.IdProducto = Convert.ToByte(vPreciosProducto[0]);
                    PrecioProducto.PrecioNivel1 = Convert.ToDecimal(vPreciosProducto[1]);
                    PrecioProducto.PrecioNivel2 = Convert.ToDecimal(vPreciosProducto[2]);

                    //Si el prodcuto no existe dentro del diccionario, lo a�ade
                    if (!Productos.ContainsKey(PrecioProducto.IdProducto))
                        Productos.Add(PrecioProducto.IdProducto, PrecioProducto);
                    else
                    {
                        Productos[PrecioProducto.IdProducto].PrecioNivel1 = PrecioProducto.PrecioNivel1;
                        Productos[PrecioProducto.IdProducto].PrecioNivel2 = PrecioProducto.PrecioNivel2;
                    }
                }

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;
                byte CaraTmp;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);



                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Eval�a si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresi�n de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;
                                //Loguea evento
                                SWRegistro.WriteLine(DateTime.Now + "|Evento|Activar TomarLecturaAperturaTurno");
                                SWRegistro.Flush();

                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            EstructuraRedSurtidor[CaraTmp].ListaGrados[0].PrecioNivel1 =
                                Productos[EstructuraRedSurtidor[CaraTmp].ListaGrados[0].IdProducto].PrecioNivel1;
                            EstructuraRedSurtidor[CaraTmp].ListaGrados[0].PrecioNivel2 =
                                Productos[EstructuraRedSurtidor[CaraTmp].ListaGrados[0].IdProducto].PrecioNivel2;
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Eval�a si la Cara a tomar las lecturas, pertenece a esta red de surtidores


                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresi�n de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;
                                //Loguea evento
                                SWRegistro.WriteLine(DateTime.Now + "|Evento|Activar TomarLecturaAperturaTurno");
                                SWRegistro.Flush();

                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            EstructuraRedSurtidor[CaraTmp].ListaGrados[0].PrecioNivel1 =
                                Productos[EstructuraRedSurtidor[CaraTmp].ListaGrados[0].IdProducto].PrecioNivel1;
                            EstructuraRedSurtidor[CaraTmp].ListaGrados[0].PrecioNivel2 =
                                Productos[EstructuraRedSurtidor[CaraTmp].ListaGrados[0].IdProducto].PrecioNivel2;
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraTmp + "|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_TurnoCerrado(string Surtidores, string PuertoTerminal)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|Evento|Recibido (TurnoCerrado). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;
                byte CaraTmp; //DCF

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //Eval�a si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresi�n de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaCierreTurno = true;
                                //Loguea evento
                                SWRegistro.WriteLine(DateTime.Now + "|Evento|Activar TomarLecturaCierreTurno");
                                SWRegistro.Flush();

                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraTmp + "|fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //Eval�a si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresi�n de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaCierreTurno = true;
                                //Loguea evento
                                SWRegistro.WriteLine(DateTime.Now + "|Evento|Activar TomarLecturaCierreTurno");
                                SWRegistro.Flush();
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraTmp + "|fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el M�todo oEvento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto)
        {
            try
            {
                Cara = ConvertirCaraBD(Cara);
                EstructuraRedSurtidor[Cara].InactivarCara = true;
                EstructuraRedSurtidor[Cara].PuertoParaImprimir = Puerto;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Evento|Recibe Inactivacion");
                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el Evento oEvento_InactivarCaraCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_FinalizarCambioTarjeta(byte Cara)
        {
            try
            {
                EstructuraRedSurtidor[Cara].ActivarCara = true;
                EstructuraRedSurtidor[Cara].Activa = true;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Evento|Recibe Activacion");
                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepci�n en el Evento oEvento_FinalizarCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_CerrarProtocolo()
        {
            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Recibe evento de detencion de Protocolo");
            SWRegistro.Flush();
            this.CondicionCiclo = false;
        }

        public void Evento_FinalizarVentaPorMonitoreoCHIP(byte Cara)
        {

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


        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera
        {
        }


        #endregion
    }
}