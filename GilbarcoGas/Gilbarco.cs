using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using POSstation.Protocolos;
//using gasolutions.Factory;

namespace gasolutions.Protocolos.Gilbarco
{
    public class Gilbarco: iProtocolo
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

        //VARIABLES DE CONTROL
        ComandoSurtidor ComandoCaras;   //Arreglo que almacena el COMANDO enviado al surtidor (Vector organizado por caras ascendentemente)
        byte CaraEncuestada;            //Cara que se esta ENCUESTANDO
        byte NumerodeCaras;             //Almacena la cantidad de caras a encuestar
        byte CaraInicial;               //Almacena la CARA INICIAL de la red de surtidores Gilbarco
        double Volumen;                 //Almacena VOLUMEN PARCIAL y VOLUMEN FINAL de la venta
        double TotalVenta;              //Almacena IMPORTE PARCIAL e IMPORTE FINAL de la venta
        double PrecioEDS;				//Almacena el PRECIO vigente en la EDS
        double LecturaTurno;            //Almacena el valor de la LECTURA tomada
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int BytesEsperados;             //Declara la cantidad de bytes esperados por Comando
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO        

        //ENUMERACIONES UTILIZADA PARA CREAR VARIABLES
        public enum EstadoCara			//Define los posibles ESTADOS de cada una de las Caras del Surtidor
        {
            Indeterminado,
            Error = 0x00,     //Codigo Verdadero 0x00, eliminado para evitar confusion cuando la variable esta recien inicializada
            Espera = 0x06,
            PorAutorizar = 0x07,
            PorReautorizar = 0x0E,
            Autorizado = 0x08,
            Despacho = 0x09,
            FinDespachoA = 0x0A,
            FinDespachoB = 0x0B,
            FinDespachoForzado = 0x0F,
            Detenido = 0x0C,
            EsperandoDatos = 0x0D
        }
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

        //ARREGLOS DE INFORMACION NECESARIA POR CARA
        EstadoCara[] EstadoCaras;       //Arreglo que almacena el ESTADO de cada una de las Caras (Vector organizado por caras ascendentemente)
        EstadoCara[] EstadoAnterior;    //Arreglo que almacena el ESTADO inmediatamente ANTERIOR de cada una de las Caras (Vector organizado por caras ascendentemente)
        bool[] PredeterminarVolumen;    //Determina el tipo de PRESET para la autorizacion de la venta
        bool[] PredeterminarValor;      //Determina si el predeterminado es de Valor
        bool[] CaraInicializada;	    //Determina si la cara ya fue inicializada
        bool[] AutorizarCara;           //Determina si la cara debe autorizarse
        //bool[] VentaFinalizada;         //Variable que determina si una venta fue liquidada o no
        bool[] TomarLecturaApertura;    //Determina si deben tomarse las lecturas para Apertura de Turno
        bool[] TomarLecturaCierre;      //Determina si deben tomarse las lecturas para Cierre de Turno
        bool[] FalloComunicacionReportado; //Indica si un nuevo error de comunicacion fue reportado
        bool[] FalloTomaLecturas;       //Variable que controla que la toma de lecturas fue adecuada
        //bool[] CaraActiva;
        bool[] ActivarCara;
        bool[] InactivarCara;
        bool[] DetenerVentaCara;
        string[] Puerto;
        string PuertoSurtidores;

        //int[] FactorDivisionPrecio;     //Almacena el factor de division para el valor de Precio obtenido del surtidor
        //int[] FactorDivisionVolumen;    //Almacena el factor de division para el valor de Volumen obtenido del surtidor
        //int[] FactorDivisionImporte;    //Almacena el factor de division para el valor de Importe obtenido del surtidor
        //int[] FactorDivisionTotalizador;//Almacena el factor de division para el valor de Totalizador obtenido del surtidor

        double[] Precio;		        //Almacena el PRECIO ACTUAL de la cara
        double[] LecturaInicialVenta;   //Almacena la LECTURA INICIAL de cada venta en curso
        double[] LecturaFinalVenta;     //Almacena la LECTURA FINAL de cada venta en curso        
        double[] ValorPredeterminado;   //Almacena el valor de PRESET para la autorizacion de la venta

        /*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        bool[] FalloComunicacion;      //Almacena el tipo de fallo de comunicacion        

        /*Tramas compuestas de bytes para comunicacion con SURTIDOR */
        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR       

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        System.Timers.Timer PollingTimer = new System.Timers.Timer(20); //Definicion del TIMER DE ENCUESTA
        SharedEvents.CMensaje oEventos;                                 //Controla la comunicacion entre las aplicaciones por medio de eventos        

        //VARIABLES VARIAS
        //Instancia Arreglo de lecturas para reportar reactivación de cara
        System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

        List<Cara> EstructuraRedSurtidor = new List<Cara>();

        //string ArchivoRegistroSucesos;      //Variable que almacen la ruta y el nombre del archivo que guarda registro de los sucesos ocurrido en la cara
        //StreamWriter SWRegistro;            //Variable utilizada para escribir en el archivo


        string ArchivoRegistroSucesos;      //Variable que almacen la ruta y el nombre del archivo que guarda registro de los sucesos ocurrido en la cara
        StreamWriter SWRegistro;            //Variable utilizada para escribir en el archivo

        //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        string ArchivoTramas;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWTramas;


        #endregion

        #region METODOS PRINCIPALES

        //PUNTO DE ARRANQUE DE LA CLASE
        public Gilbarco(string Puerto, byte NumerodeCaras, byte CaraInicial, string strPrecioEDS, List<Cara> PropiedadCara)
        {
            try
            {
                if (!Directory.Exists(Environment.CurrentDirectory + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Environment.CurrentDirectory + "/LogueoProtocolo/");
                }


                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Environment.CurrentDirectory + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tramas Gilbarco (" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                ArchivoRegistroSucesos = Environment.CurrentDirectory + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Sucesos Gilbarco (" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);


                //Escribe encabezado
                SWRegistro.WriteLine("===================|==|======|=========================================");
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo modificado 2013.03.07-1450"); //DCF control del tamaño de los archivos de logueo Sucesos y Tramas.
                SWRegistro.Flush();

                ////Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                //ArchivoRegistroSuc
                ////Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                //ArchivoRegistroSucesos = Application.ExecutablePath + "RegistroSucesosGilbarco (" + Puerto + ").txt";
                //SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                ////Escribe encabezado
                //SWRegistro.WriteLine();
                //SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.WriteLine(DateTime.Now);
                //SWRegistro.WriteLine("GILBARCO");
                //SWRegistro.WriteLine("Numero de Caras: " + NumerodeCaras + " - Cara Inicial: " + CaraInicial + " - Precio: " + strPrecioEDS);
                //SWRegistro.WriteLine("=====================================================================");
                //SWRegistro.Flush();

                //Instancia los eventos de los objetos Timer
                PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);

                //Instancia los eventos disparados por la aplicacion cliente
                Type t = Type.GetTypeFromProgID("sharedevents.CMensaje");
                oEventos = (SharedEvents.CMensaje)Activator.CreateInstance(t);
                oEventos.CambioPrecio += new SharedEvents.__CMensaje_CambioPrecioEventHandler(oEvento_CambioPrecio);
                oEventos.VentaAutorizada += new SharedEvents.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                oEventos.TurnoAbierto += new SharedEvents.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                oEventos.TurnoCerrado += new SharedEvents.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                oEventos.InactivarCaraCambioTarjeta += new SharedEvents.__CMensaje_InactivarCaraCambioTarjetaEventHandler(oEventos_InactivarCaraCambioTarjeta);
                oEventos.FinalizarCambioTarjeta += new SharedEvents.__CMensaje_FinalizarCambioTarjetaEventHandler(oEventos_FinalizarCambioTarjeta);
                oEventos.FinalizarVentaPorMonitoreoCHIP += new SharedEvents.__CMensaje_FinalizarVentaPorMonitoreoCHIPEventHandler(oEventos_FinalizarVentaPorMonitoreoCHIP);

                //Almacena el numero de caras a encuestarse
                this.NumerodeCaras = Convert.ToByte(NumerodeCaras + CaraInicial - 1);
                this.CaraInicial = CaraInicial;

                EstructuraRedSurtidor = PropiedadCara;

                //Almacena el Precio de Venta establecido para la EDS
                PrecioEDS = Convert.ToDouble(strPrecioEDS);

                PuertoSurtidores = Puerto;
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

                //Se dimensiona cada uno de los arreglos utilizados
                EstadoCaras = new EstadoCara[this.NumerodeCaras];
                EstadoAnterior = new EstadoCara[this.NumerodeCaras];

                PredeterminarVolumen = new bool[this.NumerodeCaras];
                PredeterminarValor = new bool[this.NumerodeCaras];
                CaraInicializada = new bool[this.NumerodeCaras];
                AutorizarCara = new bool[this.NumerodeCaras];
                TomarLecturaApertura = new bool[this.NumerodeCaras];
                TomarLecturaCierre = new bool[this.NumerodeCaras];
                FalloComunicacionReportado = new bool[this.NumerodeCaras];
                FalloTomaLecturas = new bool[this.NumerodeCaras];
                //CaraActiva = new bool[this.NumerodeCaras];
                ActivarCara = new bool[this.NumerodeCaras];
                InactivarCara = new bool[this.NumerodeCaras];
                DetenerVentaCara = new bool[this.NumerodeCaras];
   
                //FactorDivisionPrecio = new int[this.NumerodeCaras];
                //FactorDivisionVolumen = new int[this.NumerodeCaras];
                //FactorDivisionImporte = new int[this.NumerodeCaras];
                //FactorDivisionTotalizador = new int[this.NumerodeCaras];

                Precio = new double[this.NumerodeCaras];
                LecturaInicialVenta = new double[this.NumerodeCaras];
                LecturaFinalVenta = new double[this.NumerodeCaras];
                ValorPredeterminado = new double[this.NumerodeCaras];

                this.Puerto = new string[this.NumerodeCaras];

                FalloComunicacion = new bool[2];
                /* [0] Error en Datos
                 * [1] Error en Comunicación: Trama incompleta o no hay respuesta del surtidor*/

                //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
                PollingTimer.AutoReset = true;

                //Se activa el timer por primera vez
                PollingTimer.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Gilbarco: " + Excepcion;
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
                int MaximoReintento = 2;//Antes 5

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa el vector de control de fallo de comunicación
                FalloComunicacion[0] = false;
                FalloComunicacion[1] = false;

                //Arma la trama de Transmision
                ArmarTramaTx(ComandoaEnviar);

                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {
                    VerifySizeFile(); //DCF control del tamaño de los archivos de logueo Sucesos y Tramas.

                    EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor
                    if (BytesEsperados > 0)
                    {
                        RecibirInformacion();
                        Reintentos += 1;
                    }
                } while (((FalloComunicacion[0] == true) || (FalloComunicacion[1] == true)) && (Reintentos < MaximoReintento));

                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion[0] == true || FalloComunicacion[1] == true)
                {
                    //Si la cara se va a Inactivar
                    if (InactivarCara[CaraEncuestada - 1] == true)
                    {
                        InactivarCara[CaraEncuestada - 1] = false;
                        //CaraActiva[CaraEncuestada - 1] = false;
                        EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                        oEventos.SolicitarIniciarCambioTarjeta( CaraEncuestada,  Puerto[CaraEncuestada - 1]);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivación en Fallo de Comunicación");
                        SWRegistro.Flush();
                    }
                    //Si la cara se va a activar
                    if (ActivarCara[CaraEncuestada - 1] == true)
                    {
                        EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                        string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " con fallo de comunicación";
                        bool Imprime = true;
                        bool Terminal = false;
                        oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar activación: Fallo de comunicación");
                        SWRegistro.Flush();
                    }

                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                    {
                        string MensajeErrorLectura = "Error en Comunicación con Surtidor";
                        if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            FalloTomaLecturas[CaraEncuestada - 1] = true;
                            bool EstadoTurno = false;
                            TomarLecturaApertura[CaraEncuestada - 1] = false;
                            oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            FalloTomaLecturas[CaraEncuestada - 1] = true;
                            bool EstadoTurno = true;
                            TomarLecturaCierre[CaraEncuestada - 1] = false;
                            oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                    }

                    //Ingresa a este condicional si el surtidor NO responde y si no se ha logueado aún la falla
                    if (FalloComunicacion[1] && !FalloComunicacionReportado[CaraEncuestada - 1])
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Pérdida de comunicación. Estado: " + EstadoCaras[CaraEncuestada - 1] +
                           " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        FalloComunicacionReportado[CaraEncuestada - 1] = true;
                        //oEventos.ReportarErrorComunicacion( CaraEncuestada);                    
                    }
                    //Ingresa a este condicional cuando el surtidor responde y ya se había registrado una falla de comunicación
                    if (!FalloComunicacion[1] && FalloComunicacionReportado[CaraEncuestada - 1])
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Reestalecimiento de comunicación, pero con errores en trama. Estado: " + EstadoCaras[CaraEncuestada - 1] +
                            " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        FalloComunicacionReportado[CaraEncuestada - 1] = false;
                    }
                    //Regresa el parámetro FALSE si hubo error en la trama o en la comunicación con el surtidor
                    return false;
                }
                else
                {
                    if (FalloComunicacionReportado[CaraEncuestada - 1])
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Reestablecimiento de comunicación. Estado: " + EstadoCaras[CaraEncuestada - 1] +
                            " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        FalloComunicacionReportado[CaraEncuestada - 1] = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
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
                //Asigna a la cara a encustar el comando que fue enviado
                ComandoCaras = ComandoTx;

                /* Configuracion por defecto de los valores de TimeOut, Trama y Bytes Esperados (Aplica completamente a los
                 * Comandos Estado y Enviar Datos */
                TramaTx = new byte[1];
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
                            TimeOut = 800;
                            BytesEsperados = 94;
                            break;

                        case (ComandoSurtidor.ParcialDespacho):     //Pide Parical de Venta
                            TimeOut = 350;
                            BytesEsperados = 6;
                            break;

                        case (ComandoSurtidor.DetenerTodos):        //Detiene todos los despachos
                            TimeOut = 80;//Antes 50
                            TramaTx[0] = Convert.ToByte(ComandoTx);
                            BytesEsperados = 0;
                            break;

                        case (ComandoSurtidor.CambiarPrecio):       //Cambio de Precio
                            //Se coloca los Nibble que almacena los bytes en 0x0(TramaTx[6]-TramaTx[9]) 
                            TramaTx = new byte[13] { 0xFF, 0xE5, 0xF4, 0xF6, 0xE0, 0xF7, 0xE0, 0xE0, 0xE0, 0xE0, 0xFB, 0xE0, 0xF0 };
                            string strPrecio = Convert.ToString(PrecioEDS * EstructuraRedSurtidor[CaraEncuestada - 1].FactorPrecio).PadLeft(4, '0');
                            for (int i = 0; i <= 3; i++)
                                TramaTx[i + 6] = Convert.ToByte((Convert.ToByte(strPrecio.Substring(3 - i, 1))) | TramaTx[i + 6]);
                            TramaTx[11] = Convert.ToByte(TramaTx[11] | CalcularLRC(TramaTx, 0, 10));
                            TimeOut = 80;//Antes 50
                            BytesEsperados = 0;
                            break;

                        case (ComandoSurtidor.PredeterminarVentaDinero): //Predetermina una venta con un valor especifico de Dinero 
                            //Se coloca los Nibble que almacena los bytes de Preset en 0x00(TramaTx[6]-TramaTx[9]) 
                            TramaTx = new byte[12] { 0xFF, 0xE6, 0xF2, 0xF8, 0xE0, 0xE0, 0xE0, 0xE0, 0xE0, 0xFB, 0xE0, 0xF0 };
                            string ValoraPredeterminarDinero = Convert.ToString(ValorPredeterminado[CaraEncuestada - 1] *
                                EstructuraRedSurtidor[CaraEncuestada - 1].FactorImporte / 10).PadLeft(5, '0');
                            for (int i = 4; i <= 8; i++)
                                TramaTx[i] = Convert.ToByte((Convert.ToByte(ValoraPredeterminarDinero.Substring(8 - i, 1))) | TramaTx[i]);
                            TramaTx[10] = Convert.ToByte(TramaTx[10] | CalcularLRC(TramaTx, 0, 9));
                            TimeOut = 80;//Antes 50
                            BytesEsperados = 0;
                             break;

                        case (ComandoSurtidor.PredeterminarVentaVolumen): //Predetermina una venta con un valor especifico de Metros cubicos
                            //Se coloca los Nibble que almacena los bytes de Preset en 0x00(TramaTx[6]-TramaTx[9]) 
                            TramaTx = new byte[15] { 0xFF, 0xE3, 0xF1, 0xF4, 0xF6, 0xE0, 0xF8, 0xE0, 0xE0, 0xE0, 0xE0, 0xE0, 0xFB, 0xE0, 0xF0 };
                            string ValoraPredeterminarVolumen = Convert.ToString(ValorPredeterminado[CaraEncuestada - 1]).PadLeft(5, '0');
                            for (int i = 7; i <= 11; i++)
                                TramaTx[i] = Convert.ToByte((Convert.ToByte(ValoraPredeterminarVolumen.Substring(11 - i, 1))) | TramaTx[i]);
                            TramaTx[13] = Convert.ToByte(TramaTx[13] | CalcularLRC(TramaTx, 0, 13));
                            TimeOut = 80; //Antes 50
                            BytesEsperados = 0;

                            break;
                    }
                }
                //Variable momentanea, mientras se define como se vuelve funcional esta situacion
                eco = Convert.ToByte(TramaTx.Length);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ArmarTramaTx: " + Excepcion;
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



                ///////////////////////////////////////////////////////////////////////////////////
                ////LOGUEO DE TRAMA TRANSMITIDA
                //string strTramaTx = "";
                //for (int i = 0; i <= TramaTx.Length - 1; i++)
                //    strTramaTx += TramaTx[i].ToString("X2") + " ";

                //SWTramas.WriteLine(DateTime.Now + "Tx.Cara " + CaraEncuestada + ": " + strTramaTx);
                //SWTramas.Flush();
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

        //LEE Y ALMACENA LA TRAMA RECIBIDA
        private void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                BytesEsperados = BytesEsperados + eco;

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

                    /////////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA RECIBIDA
                    //string strTramaRx = "";
                    //for (int i = 0; i <= TramaRx.Length - 1; i++)
                    //    strTramaRx += TramaRx[i].ToString("X2") + " ";

                    //SWTramas.WriteLine(DateTime.Now + "Rx.Cara " + CaraEncuestada + ": " + strTramaRx);
                    //SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////


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
                        "|" + CaraEncuestada + "|Rx|" + strTrama);
                    SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////


                    AnalizarTrama();
                }
                else if (FalloComunicacion[1] == false)
                    FalloComunicacion[1] = true;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
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
                string MensajeExcepcion = "Excepción en el Método AnalizarTrama: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void AsignarEstado()
        {
            try
            {
                //Se separan el Codigo del estado y la cara en variables diferentes.  La "e" es el parametro aditivo del ECO recibido
                byte CodigoEstado = Convert.ToByte(TramaRx[0] & (0xF0));//Eco

                //Almacena ultimo estado si este no es indeterminado
                //if ((EstadoCaras[CaraEncuestada - 1] != EstadoCara.Indeterminado) || (EstadoAnterior[CaraEncuestada - 1] != EstadoCara.Despacho))
                //{
                if (EstadoAnterior[CaraEncuestada - 1] != EstadoCaras[CaraEncuestada - 1])
                    EstadoAnterior[CaraEncuestada - 1] = EstadoCaras[CaraEncuestada - 1];
                //}
                byte CaraqueResponde = Convert.ToByte(TramaRx[0] & (0x0F));//Eco
                //Evalua si la informacion que se recibio como respuesta corresponde a la cara que fue encuestada
                if (CaraqueResponde == CaraEncuestada)
                {
                    FalloComunicacion[0] = false; //No hubo error por fallas en datos
                    //Asigna Estado
                    switch (CodigoEstado)
                    {
                        case (0x00):
                            EstadoCaras[CaraEncuestada - 1] = EstadoCara.Error;
                            break;
                        case (0x60):
                            /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                            if (EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial == true)
                            {
                                EstadoCaras[CaraEncuestada - 1] = EstadoCara.FinDespachoForzado;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|Finaliza venta en Estado Espera");
                                SWRegistro.Flush();
                            }
                            /*--*/
                            else
                                EstadoCaras[CaraEncuestada - 1] = EstadoCara.Espera;
                            break;
                        case (0x70):
                            /* CASO ESPCIAL KRAUS: En caso que el surtidor se detenga y cambie su estado a Requiere Autorizacion*/
                            //----------------------------------------------------------------------------------------------------------------------//
                            if ((EstadoCaras[CaraEncuestada - 1] == EstadoCara.Despacho) || (EstadoCaras[CaraEncuestada - 1] == EstadoCara.PorReautorizar))
                                EstadoCaras[CaraEncuestada - 1] = EstadoCara.PorReautorizar;
                            //------------------------------------------------------------------------------------------------------------------------//
                            else
                            {
                                /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                                if (EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial == true)
                                {
                                    EstadoCaras[CaraEncuestada - 1] = EstadoCara.FinDespachoForzado;
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Pide autorización con estado anterior: " +
                                        EstadoCaras[CaraEncuestada - 1]);
                                    SWRegistro.Flush();
                                }
                                /*--*/
                                else
                                    EstadoCaras[CaraEncuestada - 1] = EstadoCara.PorAutorizar;
                            }
                            break;
                        case (0x80):
                            EstadoCaras[CaraEncuestada - 1] = EstadoCara.Autorizado;
                            break;
                        case (0x90):
                            EstadoCaras[CaraEncuestada - 1] = EstadoCara.Despacho;
                            break;
                        case (0xA0):
                            EstadoCaras[CaraEncuestada - 1] = EstadoCara.FinDespachoA;
                            break;
                        case (0xB0):
                            EstadoCaras[CaraEncuestada - 1] = EstadoCara.FinDespachoB;
                            break;
                        case (0xC0):
                            EstadoCaras[CaraEncuestada - 1] = EstadoCara.Detenido;
                            break;
                        case (0xD0):
                            EstadoCaras[CaraEncuestada - 1] = EstadoCara.EsperandoDatos;
                            break;
                        default:
                            //EstadoCaras[CaraEncuestada - 1] = EstadoCara.Indeterminado;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|EstadoIndeterminado: " + CodigoEstado +
                            " - Comando enviado: " + ComandoCaras);
                            SWRegistro.Flush();
                            FalloComunicacion[0] = true;
                            break;
                    }

                    //Almacena en archivo el estado actual del surtidor
                    if (EstadoAnterior[CaraEncuestada - 1] != EstadoCaras[CaraEncuestada - 1])
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|" + EstadoCaras[CaraEncuestada - 1]);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + EstructuraRedSurtidor[CaraEncuestada - 1].IdCara + "|Error|Comando: " + ComandoCaras + " - Cara que Responde: " + CaraqueResponde);
                    SWRegistro.Flush();
                    FalloComunicacion[0] = true;
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
                //Solamente ingresa a esta parte de código cuando no se ha inicializado la cara (inicio de programa)
                if (CaraInicializada[CaraEncuestada - 1] == false)
                {
                    //Factor de división por defecto del Precio
                    if (EstructuraRedSurtidor[CaraEncuestada - 1].FactorPrecio == 0)
                        EstructuraRedSurtidor[CaraEncuestada - 1].FactorPrecio = 1;

                    //Factor de división por defecto del TotalVolumen
                    if (EstructuraRedSurtidor[CaraEncuestada - 1].FactorVolumen == 0)
                        EstructuraRedSurtidor[CaraEncuestada - 1].FactorVolumen = 1000;

                    //Factor de división por defecto del TotalDinero 
                    if (EstructuraRedSurtidor[CaraEncuestada - 1].FactorImporte == 0)
                        EstructuraRedSurtidor[CaraEncuestada - 1].FactorImporte = 10;

                    //Factor de división por defecto del Totalizadores
                    if (EstructuraRedSurtidor[CaraEncuestada - 1].FactorTotalizador == 0)
                        EstructuraRedSurtidor[CaraEncuestada - 1].FactorTotalizador = 100;

                    //Si hay una venta en VentaParcial almacena la LecturaInicial obtenida en la tabla
                    if (EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial)
                    {
                        LecturaInicialVenta[CaraEncuestada - 1] =
                            Convert.ToDouble(EstructuraRedSurtidor[CaraEncuestada - 1].LecturaInicialVParcial);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Asume Lectura Inicial de Venta de VentaParcial. Lectura: " +
                            LecturaInicialVenta[CaraEncuestada - 1]);
                        SWRegistro.Flush();
                    }
                    CaraInicializada[CaraEncuestada - 1] = true;

                    //Estado indeterminado, ya que recien se ejecuta el programa
                    //EstadoCaras[CaraEncuestada - 1] = EstadoCara.Indeterminado;
                    //EstadoAnterior[CaraEncuestada - 1] = EstadoCara.Indeterminado;

                    //Si la cara esta en reposo
                    //if ((EstadoCaras[CaraEncuestada - 1] == EstadoCara.Espera) || (EstadoCaras[CaraEncuestada - 1] == EstadoCara.PorAutorizar))
                    //{
                    //    //Realiza cambio de PRECIO BASE
                    //    CambiarPrecio();

                    //    //Cambia bandera inidicando que la cara fue inicializada correctamente
                    //    CaraInicializada[CaraEncuestada - 1] = true;
                    //}
                }

                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (EstadoCaras[CaraEncuestada - 1])
                {
                    case (EstadoCara.Espera):
                        DetenerVentaCara[CaraEncuestada - 1] = false;

                        //Si la cara se va a Inactivar
                        if (InactivarCara[CaraEncuestada - 1] == true)
                        {
                            InactivarCara[CaraEncuestada - 1] = false;
                            //CaraActiva[CaraEncuestada - 1] = false;
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            oEventos.SolicitarIniciarCambioTarjeta( CaraEncuestada,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivación en Estado Espera");
                            SWRegistro.Flush();

                            //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante inactivación
                            if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                            {
                                string MensajeErrorLectura = "Cara Inactivada";
                                if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    FalloTomaLecturas[CaraEncuestada - 1] = true;
                                    bool EstadoTurno = false;
                                    TomarLecturaApertura[CaraEncuestada - 1] = false;
                                    oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                                if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    FalloTomaLecturas[CaraEncuestada - 1] = true;
                                    bool EstadoTurno = true;
                                    TomarLecturaCierre[CaraEncuestada - 1] = false;
                                    oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                            }

                            //Sale del Caso si se inactiva
                            break;
                        }

                        //Si la cara se va a activar
                        if (ActivarCara[CaraEncuestada - 1] == true)
                        {
                            if (TomarLecturaActivacionCara())
                            {
                                //Instancia Array para reportar las lecturas
                                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                                ArrayLecturas.CopyTo(LecturasEnvio);
                                //Lanza Evento para reportar las lecturas después de un cambio de tarjeta
                                oEventos.SolicitarLecturasCambioTarjeta( LecturasEnvio);
                                //Inicializa bandera que indica la activación de una cara
                                ActivarCara[CaraEncuestada - 1] = false;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Activación en Estado Espera. Lectura: " + LecturasEnvio);
                                SWRegistro.Flush();
                            }
                        }

                        //Informa cambio de estado
                        if (EstadoAnterior[CaraEncuestada - 1] != EstadoCaras[CaraEncuestada - 1])
                        {
                            oEventos.InformarCaraEnReposo( CaraEncuestada);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara en Espera");
                            SWRegistro.Flush();
                            //Si había venta por predeterminar, al colgar la manguera el sistema cancela el proceso de predeterminado
                            if (PredeterminarVolumen[CaraEncuestada - 1] == true)
                                PredeterminarVolumen[CaraEncuestada - 1] = false;
                            if (PredeterminarValor[CaraEncuestada - 1] == true)
                                PredeterminarValor[CaraEncuestada - 1] = false;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (AutorizarCara[CaraEncuestada - 1] == true)
                            AutorizarCara[CaraEncuestada - 1] = false;

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if ((TomarLecturaApertura[CaraEncuestada - 1] == true) || (TomarLecturaCierre[CaraEncuestada - 1] == true))
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia toma de lecturas para cierre o apertura");
                            SWRegistro.Flush();
                            LecturaAperturaCierre();
                        }
                        //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                        /*if (PrecioEDS != Precio[CaraEncuestada - 1])
                            CambiarPrecio();*/
                        break;

                    case (EstadoCara.Despacho):
                        //Si la cara se va a Inactivar
                        if (InactivarCara[CaraEncuestada - 1] == true)
                        {
                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            InactivarCara[CaraEncuestada - 1] = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Inactivación: Cara en despacho");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (ActivarCara[CaraEncuestada - 1] == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Activación: Cara en despacho");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho                    
                        if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = false;
                                TomarLecturaApertura[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = true;
                                TomarLecturaCierre[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (AutorizarCara[CaraEncuestada - 1] == true)
                            AutorizarCara[CaraEncuestada - 1] = false;

                        /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial == false)
                            EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial = true;
                        /*--*/


                        if (DetenerVentaCara[CaraEncuestada - 1])
                        {
                            DetenerVentaCara[CaraEncuestada - 1] = false;
                            ProcesoEnvioComando(ComandoSurtidor.Detener);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Envia comando de Detener Venta");
                            SWRegistro.Flush();
                        }

                        //Pedir Parciales de Venta
                        ArmarTramaTx(ComandoSurtidor.ParcialDespacho);
                        EnviarComando();
                        RecibirInformacion();   

                        //Dispara evento al programa principal si no hubo fallo en la recepcion de los datos
                        if (FalloComunicacion[1] == false)
                        {
                            string strTotalVenta = TotalVenta.ToString("N2");
                            string strVolumen = Volumen.ToString("N2");
                            oEventos.InformarVentaParcial( CaraEncuestada,  strTotalVenta,  strVolumen);
                        }

                        break;

                    case (EstadoCara.Detenido):
                        DetenerVentaCara[CaraEncuestada - 1] = false;

                        if ((EstadoAnterior[CaraEncuestada - 1] != EstadoCaras[CaraEncuestada - 1]))
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|Detenida");
                            SWRegistro.Flush();
                        }
                        //Si la cara se va a Inactivar
                        if (InactivarCara[CaraEncuestada - 1] == true)
                        {
                            InactivarCara[CaraEncuestada - 1] = false;
                            //CaraActiva[CaraEncuestada - 1] = false;
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            oEventos.SolicitarIniciarCambioTarjeta( CaraEncuestada,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivación en Estado Detenido");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (ActivarCara[CaraEncuestada - 1] == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en estado Detenido";
                            bool Imprime = true;
                            bool Terminal = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar activación: Cara en estado Detenido");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho                    
                        if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                        {
                            string MensajeErrorLectura = "Cara Detenida";
                            if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = false;
                                TomarLecturaApertura[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = true;
                                TomarLecturaCierre[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }
                        break;
                    case (EstadoCara.FinDespachoForzado):
                    case (EstadoCara.FinDespachoA):
                    case (EstadoCara.FinDespachoB):
                        //Si la cara se va a Inactivar
                        if (InactivarCara[CaraEncuestada - 1] == true)
                        {
                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en Fin de Venta";
                            bool Imprime = true;
                            bool Terminal = false;
                            InactivarCara[CaraEncuestada - 1] = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Inactivación: Cara en estado Fin de Venta");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (ActivarCara[CaraEncuestada - 1] == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en Fin de Despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Activación: Cara en estado Fin de Venta");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho                    
                        if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                        {
                            string MensajeErrorLectura = "Cara en Fin de Despacho";
                            if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = false;
                                TomarLecturaApertura[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = true;
                                TomarLecturaCierre[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                        //Si la venta no ha sido finalizada, se ejecuta proceso para finalizarla
                        if (EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial == true)
                            ProcesoFindeVenta();
                        /*--*/

                        break;

                    case (EstadoCara.Error):
                        //Si la cara se va a Inactivar
                        if (InactivarCara[CaraEncuestada - 1] == true)
                        {
                            InactivarCara[CaraEncuestada - 1] = false;
                            //CaraActiva[CaraEncuestada - 1] = false;
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            oEventos.SolicitarIniciarCambioTarjeta( CaraEncuestada,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Inactivación en Estado de Error");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (ActivarCara[CaraEncuestada - 1] == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en estado de Error";
                            bool Imprime = true;
                            bool Terminal = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar activación: Cara en estado de Error");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                        {
                            string MensajeErrorLectura = "Cara en estado de ERROR";
                            if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = false;
                                TomarLecturaApertura[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = true;
                                TomarLecturaCierre[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }
                        break;

                    case (EstadoCara.PorAutorizar):
                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        /*if ((TomarLecturaApertura[CaraEncuestada - 1] == true) || (TomarLecturaCierre[CaraEncuestada - 1] == true))
                            LecturaAperturaCierre();*/

                        //Si la cara se va a Inactivar
                        if (InactivarCara[CaraEncuestada - 1] == true)
                        {
                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en intento de autorización";
                            bool Imprime = true;
                            bool Terminal = false;
                            InactivarCara[CaraEncuestada - 1] = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Inactivación: Cara en estado Por Autorizar");
                            SWRegistro.Flush();
                            break;
                        }

                        //Si la cara se va a activar
                        if (ActivarCara[CaraEncuestada - 1] == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en estado Por Autorizar";
                            bool Imprime = true;
                            bool Terminal = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Activación: Cara en estado Por Autorizar");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                        {
                            string MensajeErrorLectura = "Manguera descolgada";
                            if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = false;
                                TomarLecturaApertura[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = true;
                                TomarLecturaCierre[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                        //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                        if (EstadoAnterior[CaraEncuestada - 1] != EstadoCaras[CaraEncuestada - 1] &&
                            EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial == false)
                        {
                            oEventos.RequerirAutorizacion( CaraEncuestada);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion");
                            SWRegistro.Flush();
                        }
                        /*--*/

                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        if (AutorizarCara[CaraEncuestada - 1] == true)
                        {
                            //Obtiene la Lectura Inicial de la Venta
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura Inicial de Venta");
                            SWRegistro.Flush();
                            TomarLecturas();
                            if (LecturaTurno > 0)
                                LecturaInicialVenta[CaraEncuestada - 1] = LecturaTurno;
                            else
                            {
                                LecturaInicialVenta[CaraEncuestada - 1] = LecturaFinalVenta[CaraEncuestada - 1];
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura inicial en 0, asume Lectura Final");
                                SWRegistro.Flush();
                            }

                            string strLecturasVolumen = LecturaInicialVenta[CaraEncuestada - 1].ToString("N3");
                            oEventos.InformarLecturaInicialVenta( CaraEncuestada,  strLecturasVolumen);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Inicial de Venta: " + LecturaInicialVenta[CaraEncuestada - 1]);
                            SWRegistro.Flush();

                            //Si la siguiente venta es predeterminada, realiza el proceso de programación
                            if (PredeterminarVolumen[CaraEncuestada - 1] == true || PredeterminarValor[CaraEncuestada - 1] == true)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Programación de Venta");
                                SWRegistro.Flush();
                                Predeterminar();
                            }
                            //Envía comando de Autorización
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Autorización de Venta");
                            SWRegistro.Flush();
                            Reintentos = 0;
                            do
                            {
                                ProcesoEnvioComando(ComandoSurtidor.Autorizar);
                                Reintentos++;
                                Thread.Sleep(30);
                                ProcesoEnvioComando(ComandoSurtidor.Estado);
                            } while ((EstadoCaras[CaraEncuestada - 1] != EstadoCara.Autorizado) && (EstadoCaras[CaraEncuestada - 1] != EstadoCara.Despacho) && (Reintentos <= 3));

                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            if (EstadoCaras[CaraEncuestada - 1] != EstadoCara.Autorizado ||
                                EstadoCaras[CaraEncuestada - 1] != EstadoCara.Despacho)
                            {
                                AutorizarCara[CaraEncuestada - 1] = false;
                                /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                                EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial = true;
                                /*--*/
                            }
                        }

                        break;
                    case (EstadoCara.PorReautorizar):
                        //Si la cara se va a Inactivar
                        if (InactivarCara[CaraEncuestada - 1] == true)
                        {
                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en intento de Reautorización";
                            bool Imprime = true;
                            bool Terminal = false;
                            InactivarCara[CaraEncuestada - 1] = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Inactivación: Cara en Estado de Reautorización");
                            SWRegistro.Flush();
                        }

                        //Si la cara se va a activar
                        if (ActivarCara[CaraEncuestada - 1] == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada - 1].Activa = false;
                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en estado Por Reautorizar";
                            bool Imprime = true;
                            bool Terminal = false;
                            oEventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  Puerto[CaraEncuestada - 1]);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se puede ejecutar Activación: Cara en Estado de Reautorización");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Reautorización
                        if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                        {
                            string MensajeErrorLectura = "Cara en Despacho/Reautorización";
                            if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = false;
                                TomarLecturaApertura[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = true;
                                TomarLecturaCierre[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        /*do
                        {
                            EnviarComando(ComandoSurtidor.Autorizar);
                            EnviarComando(ComandoSurtidor.Estado);
                            RecibirInformacion();
                        } while ((EstadoCaras[CaraEncuestada - 1] != EstadoCara.Autorizado) && (EstadoCaras[CaraEncuestada - 1] != EstadoCara.Despacho) && (Reintentos <= 3));*/
                        if (EstadoCaras[CaraEncuestada - 1] != EstadoAnterior[CaraEncuestada - 1])
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|Por Reautorizar");
                            SWRegistro.Flush();
                        }
                        break;

                    default:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Reautorización
                        if (FalloTomaLecturas[CaraEncuestada - 1] == false)
                        {
                            string MensajeErrorLectura = "Cara no colgada (estado indeterminado)";
                            if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = false;
                                TomarLecturaApertura[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                FalloTomaLecturas[CaraEncuestada - 1] = true;
                                bool EstadoTurno = true;
                                TomarLecturaCierre[CaraEncuestada - 1] = false;
                                oEventos.ReportarCancelacionTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }
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

        //REALIZA PROCESO DE FIN DE VENTA
        private void ProcesoFindeVenta()
        {
            try
            {
                //Inicializacion de variables
                Volumen = 0;
                TotalVenta = 0;
                Precio[CaraEncuestada - 1] = 0;
                //int Reintentos = 0;
                double VolumenCalculado;

                //Obtiene los Valores Finales de la Venta (Pesos y Metros cubicos despachados)
                if (ProcesoEnvioComando(ComandoSurtidor.TotalDespacho))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura Final de Venta");
                    SWRegistro.Flush();
                    //Obtiene la Lectura Final de la Venta
                    TomarLecturas();
                    LecturaFinalVenta[CaraEncuestada - 1] = LecturaTurno;

                    //Calcula el volumen despachado según lecturas Inicial y Final de venta
                    VolumenCalculado = LecturaFinalVenta[CaraEncuestada - 1] - LecturaInicialVenta[CaraEncuestada - 1];

                    //Realiza comparación entre volumen calculado por lecturas y volumen obtenido por finalización de venta
                    // Tiene en cuenta si se reiniciaron las lecturas por secuencia normal del Totalizador del surtidor
                    if (VolumenCalculado >= 0)
                    {
                        //Si no se ha reiniciado el sistema, el valor de LecturaInicial es diferente de 0
                        if (LecturaInicialVenta[CaraEncuestada - 1] > 0)
                        {
                            if (LecturaFinalVenta[CaraEncuestada - 1] > 0)
                            {
                                /*Se compara el valor de Volumen Calculado con el valor de Volumen Recibido.
                                 * La diferencia no debe exceder el (+/-) 1%.  
                                 * Se da mayor credibilidad al calculado por lecturas*/
                                if (Volumen < VolumenCalculado - 0.5 || Volumen > VolumenCalculado + 0.5)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Volumen Calculado: " + VolumenCalculado + " - Volumen Reportado: " + Volumen);
                                    SWRegistro.Flush();
                                    Volumen = VolumenCalculado;
                                    if (Precio[CaraEncuestada - 1] == 0)
                                        TotalVenta = Volumen * PrecioEDS;
                                    else
                                        TotalVenta = Volumen * Precio[CaraEncuestada - 1];
                                }
                            }
                            else
                            {
                                LecturaFinalVenta[CaraEncuestada - 1] = LecturaInicialVenta[CaraEncuestada - 1] + Volumen;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Lectura Final de Venta en 0. Calculada: " +
                                    LecturaFinalVenta[CaraEncuestada - 1]);
                                SWRegistro.Flush();
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Lectura Inicial en 0");
                            SWRegistro.Flush();
                        }
                    }

                    //Si se realizó una venta con valores de m3 y $ mayor que cero
                    if (Volumen != 0)
                    {
                        if (TotalVenta == 0)
                        {
                            if (Precio[CaraEncuestada - 1] == 0)
                                TotalVenta = Volumen * PrecioEDS;
                            else
                                TotalVenta = Volumen * Precio[CaraEncuestada - 1];
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Importe en 0. Calculado " + TotalVenta);
                            SWRegistro.Flush();
                        }

                        //Dispara evento al programa principal si la venta es diferente de 0
                        string strTotalVenta = TotalVenta.ToString("N2");
                        string strPrecio = Precio[CaraEncuestada - 1].ToString("N2");
                        string strLecturaFinalVenta = LecturaFinalVenta[CaraEncuestada - 1].ToString("N3");
                        string strVolumen = Volumen.ToString("N2");
                        byte bytProducto = 1;

                        //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, setea bandera indicadora de Venta Finalizada
                        /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                        EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial = false;
                        /*--*/
                        string PresionLlenado = "0";

                        oEventos.InformarFinalizacionVenta( CaraEncuestada,  strTotalVenta,  strPrecio,  strLecturaFinalVenta,  strVolumen,  bytProducto,  PresionLlenado);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa fin de venta: Importe: " + strTotalVenta +
                            " - Precio: " + strPrecio + " - Lectura Final: " + strLecturaFinalVenta + " - Volumen: " + strVolumen + " - Presión: " + PresionLlenado);
                        SWRegistro.Flush();
                    }
                    /*- Fecha de Inclusión: 2008/03/18 12:00 -*/
                    else
                    {
                        oEventos.ReportarVentaEnCero( CaraEncuestada);
                        EstructuraRedSurtidor[CaraEncuestada - 1].EsVentaParcial = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Venta en CERO");
                        SWRegistro.Flush();
                    }
                    /*--*/
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ProcesoFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //OBTIENE LOS VALORES FINALES DE UNA VENTA
        private void RecuperarDatosFindeVenta()
        {
            try
            {
                //Calcula el LRC
                int LRCCalculado = CalcularLRC(TramaRx, 0, (TramaRx.Length - 3));//Eco
                int LRCObtenidoEnTama = TramaRx[(TramaRx.Length - 2)] & 0x0F;

                //Si el LRC Recibido (TramaRx[TramaRx.Length - 2] AND 0x0F) es igual al calculado
                if (LRCObtenidoEnTama == LRCCalculado)//Eco
                {
                    byte CaraqueResponde = Convert.ToByte((TramaRx[4] & (0x0F)) + 1);//Eco
                    if (CaraqueResponde == CaraEncuestada)
                    {
                        //Se obtiene el Precio con que se realizo la venta
                        Precio[CaraEncuestada - 1] = ObtenerValor(12, 15) / EstructuraRedSurtidor[CaraEncuestada - 1].FactorPrecio;//Eco

                        //Se obtiene el Volumen despachado
                        Volumen = ObtenerValor(17, 22) / EstructuraRedSurtidor[CaraEncuestada - 1].FactorVolumen;//Eco

                        //Se obtiene el Dinero despachado
                        TotalVenta = ObtenerValor(24, 29) / EstructuraRedSurtidor[CaraEncuestada - 1].FactorImporte;//Eco

                        //No hubo error por fallas en datos
                        FalloComunicacion[0] = false;
                    }
                    else
                    {
                        FalloComunicacion[0] = true;
                        SWRegistro.WriteLine(DateTime.Now + "|" + EstructuraRedSurtidor[CaraEncuestada - 1].IdCara + "|Error|Comando " + ComandoCaras
                            + " no corresponde a Cara que responde: " + CaraqueResponde);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    FalloComunicacion[0] = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Comando " + ComandoCaras + " responde LRC Errado. LRC Obtenido: " +
                        LRCObtenidoEnTama + " - LRC Calculado: " + LRCCalculado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecuperarDatosFindeVenta: " + Excepcion;
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
                TomarLecturas();
                //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                if (TomarLecturaCierre[CaraEncuestada - 1] == true)
                {
                    string strLecturasVolumen = LecturaTurno.ToString("N3");
                    oEventos.InformarLecturaFinalTurno( CaraEncuestada,  strLecturasVolumen);
                    TomarLecturaCierre[CaraEncuestada - 1] = false;

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Final Turno: " + strLecturasVolumen);
                    SWRegistro.Flush(); 
                }
                //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                if (TomarLecturaApertura[CaraEncuestada - 1] == true)
                {
                    string strLecturasVolumen = LecturaTurno.ToString("N3");
                    oEventos.InformarLecturaInicialTurno( CaraEncuestada,  strLecturasVolumen);
                    TomarLecturaApertura[CaraEncuestada - 1] = false;

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lectura Inicial Turno: " + strLecturasVolumen);
                    SWRegistro.Flush(); 

                    //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                    if (PrecioEDS != Precio[CaraEncuestada - 1])
                        CambiarPrecio();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
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
                LecturaTurno = 0;

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.Totales))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Surtidor no respondió a comando Toma de Lectura");
                        SWRegistro.Flush();
                    }
                } while ((LecturaTurno == 0) && Reintentos <= 3);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método TomarLecturas: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
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
                    Precio[CaraEncuestada - 1] = 0;

                    //Obtiene todos los valores de precio y lecturas de la cara
                    LecturaTurno = ObtenerValor(4, 11) / EstructuraRedSurtidor[CaraEncuestada - 1].FactorTotalizador;//Eco
                    Precio[CaraEncuestada - 1] = ObtenerValor(22, 25) / EstructuraRedSurtidor[CaraEncuestada - 1].FactorPrecio;//Eco
                    FalloComunicacion[0] = false;
                }
                else
                {
                    FalloComunicacion[0] = true;
                    SWRegistro.WriteLine(DateTime.Now + "" + CaraEncuestada + "|Error|Comando " + ComandoCaras + " responde LRC Errado. LRC Obtenido: " +
                       LRCObtenidoEnTrama + " - LRC Calculado: " + LRCCalculado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecuperarTotalizadores: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //OBTIENE EL VALOR EN PESOS DE LA VENTA EN CURSO Y CALCULA A PARTIR DE ESTE Y EL PRECIO EL VALOR DE VOLUMEN
        private void RecuperarParcialesdeVenta()
        {
            try
            {
                TotalVenta = ObtenerValor(0, 5) / EstructuraRedSurtidor[CaraEncuestada - 1].FactorImporte;//Eco
                if (CaraInicializada[CaraEncuestada - 1] == true)
                    Volumen = TotalVenta / Precio[CaraEncuestada - 1];
                else
                    Volumen = TotalVenta / PrecioEDS;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método RecuperarParcialesdeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EVALUA SI LA CARA ENVIO CONFIRMACION PARA ENVIO DE DATOS
        private void ConfirmacionEnvioDatos()
        {
            try
            {
                //Almacena el Nibble Respuesta
                byte Respuesta = Convert.ToByte(TramaRx[0] & (0xF0));//Eco

                FalloComunicacion[0] = false;

                //Se evalua si el Surtidor esta preparado para recibir los datos
                if (Respuesta == 0xD0)
                {
                    if (Convert.ToByte(TramaRx[0] & (0x0F)) != CaraEncuestada)//Eco
                    {
                        FalloComunicacion[0] = true;
                        SWRegistro.WriteLine(DateTime.Now + "|" + EstructuraRedSurtidor[CaraEncuestada - 1].IdCara + "|Error|Comando " + ComandoCaras
                            + " no corresponde a Cara que responde: " + Convert.ToByte(TramaRx[0] & (0x0F)));
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    FalloComunicacion[0] = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Comando " + ComandoCaras +
                        " Respuesta errónea recibida: " + TramaRx[0]);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ConfirmaciónEnvioDatos: " + Excepcion;
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
                ProcesoEnvioComando(ComandoSurtidor.Totales);

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Cambio de precio");
                SWRegistro.Flush();
                //Analiza si se debe cambiar el precio base de la cara
                if (Precio[CaraEncuestada - 1] != PrecioEDS)
                {
                    int Reintentos = 0;
                    do
                    {
                        if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos))
                        {
                            ArmarTramaTx(ComandoSurtidor.CambiarPrecio);
                            EnviarComando();
                            ProcesoEnvioComando(ComandoSurtidor.Totales);
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada +
                                "|Error|No aceptó comando Envío de datos para cambio de precio");
                            SWRegistro.Flush();
                        }

                        Reintentos += 1;
                    } while ((Precio[CaraEncuestada - 1] != PrecioEDS) && (Reintentos <= 3));

                    if (Precio[CaraEncuestada - 1] != PrecioEDS)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No se pudo establecer nuevo precio");
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Precio establecido exitosamente");
                        SWRegistro.Flush();
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método CambiarPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        private bool TomarLecturaActivacionCara()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                LecturaTurno = 0;

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.Totales))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada +
                            "|Error|Surtidor no respondió a comando Toma de Lectura para Activación de Cara");
                        SWRegistro.Flush();
                        //Si el proceso no fue exitoso, la función devuelve False
                        return false;
                    }
                } while ((LecturaTurno == 0) && Reintentos <= 3);

                if (LecturaTurno == 0)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Lectura para Activación de Cra recibida en CERO");
                    SWRegistro.Flush();
                }

                try
                {
                    ArrayLecturas = new System.Collections.ArrayList();
                    ArrayLecturas.Add(CaraEncuestada + "|" + LecturaTurno);
                }
                catch (Exception ex)
                {
                    string MensajeExcepcion = "Excepción en el armado de ArrayLecturas Método TomarLecturaActivacionCara: " + ex;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                    SWRegistro.Flush();
                }

                //Si el proceso de toma de lecturas fue exitoso, devuelve True
                return true;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método TomarLecturaActivacionCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //REALIZA PROCESO PARA PREDETERMINAR UNA VENTA (POR METROS CUBICOS O POR DINERO)
        public void Predeterminar()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos))
                {
                    if (PredeterminarVolumen[CaraEncuestada - 1] == true)
                    {
                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen);
                        PredeterminarVolumen[CaraEncuestada - 1] = false;
                    }
                    if (PredeterminarValor[CaraEncuestada - 1] == true)
                    {
                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaDinero);
                        PredeterminarValor[CaraEncuestada - 1] = false;
                    }
                    EnviarComando();
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No aceptó comando Envío de datos para Predeterminar");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método Predeterminar";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        #endregion


        public void VerifySizeFile() //Logueo
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                    ArchivoTramas = Environment.CurrentDirectory + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tramas Gilbarco (" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }

                FileInf = new FileInfo(ArchivoRegistroSucesos);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar incosistencias o errores de logica o comunicacion
                    ArchivoRegistroSucesos = Environment.CurrentDirectory + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Sucesos Gilbarco (" + Puerto + ").txt";
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
                string MensajeExcepcion = "Excepción en el Método CalcularLRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 1;
            }
        }

        private double ObtenerValor(int PosicionInicial, int PosicionFinal)
        {
            try
            {
                double Valor = new double();
                for (int i = PosicionInicial; i <= PosicionFinal; i++)
                    Valor += (TramaRx[i] & 0x0F) * Math.Pow(10, i - PosicionInicial);
                return Valor;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ObtenerValor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 1;
            }
        }

        //INICIALIZA VALORES DE LA MATRIZ PARA TOMA DE LECTURAS
        private void IniciaTomaLecturasTurno(string Surtidores, bool Apertura)
        {
            try
            {
                string[] bSurtidores = Surtidores.Split('|');
                int CaraLectura;
                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(bSurtidores[i]) * 2 - 1;

                        //Si la cara esta en la red
                        if (CaraInicial <= CaraLectura && CaraLectura <= NumerodeCaras)
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            FalloTomaLecturas[CaraLectura - 1] = false;

                            if (Apertura)
                                TomarLecturaApertura[CaraLectura - 1] = true;    //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                            else
                                TomarLecturaCierre[CaraLectura - 1] = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales

                            //Organiza banderas de pedido de lecturas para la cara PAR
                            CaraLectura = Convert.ToByte(bSurtidores[i]) * 2;

                            //Setea la variable de impresión de Fallo de toma lectura
                            FalloTomaLecturas[CaraLectura - 1] = false;

                            if (Apertura)
                                TomarLecturaApertura[CaraLectura - 1] = true;     //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                            else
                                TomarLecturaCierre[CaraLectura - 1] = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                        }
                        //else
                        //{
                        //    SW.WriteLine(DateTime.Now + "  Cara " + CaraLectura + " fuera de red de surtidores. Método: IniciaTomaLecturasTurno");
                        //    SW.Flush();
                        //}
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

        #region EVENTOS DE LA CLASE

        //SE EJECUTA CADA PERIODO DE TIEMPO
        private void PollingTimerEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                //Se detiene el timer para realizar el respectivo proceso de encuesta
                PollingTimer.Stop();

                //Evalua la cara a encuestar. Si ya termino el recorrido, repite el ciclo
                if (CaraEncuestada >= NumerodeCaras)
                    CaraEncuestada = CaraInicial;
                else
                    CaraEncuestada += 1;

                //Encuesta caras Activas para determinar estado y toma accion sobre el estado si no hay error en los datos durante la comunicacion
                //if (CaraActiva[CaraEncuestada - 1] == true)
                if (EstructuraRedSurtidor[CaraEncuestada - 1].Activa == true)
                {
                    //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                    if (ProcesoEnvioComando(ComandoSurtidor.Estado))
                        TomarAccion();
                }
                //Luego de realizado el proceso se reactiva el Timer
                PollingTimer.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento PollingTimerEvent: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEvento_CambioPrecio( byte Cara,  string Valor)
        {
            try
            {
                PrecioEDS = Convert.ToDouble(Valor);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_CambioPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEvento_VentaAutorizada( byte Cara,  string Precio,  string ValorProgramado,  byte TipoProgramacion,  string Placa)
        {
            try
            {
                if (CaraInicial <= Cara && Cara <= NumerodeCaras)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorización. Valor Programado " + ValorProgramado +
                        " - Tipo de Programación: " + TipoProgramacion);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para desapchar
                    AutorizarCara[Cara - 1] = true;

                    ValorPredeterminado[Cara - 1] = Convert.ToDouble(ValorProgramado);

                    //Si viene valor para predeterminar setea banderas
                    if (ValorPredeterminado[Cara - 1] != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            PredeterminarVolumen[Cara - 1] = true;
                            PredeterminarValor[Cara - 1] = false;
                        }
                        else
                        {
                            PredeterminarVolumen[Cara - 1] = false;
                            PredeterminarValor[Cara - 1] = true;
                        }
                    }
                    else
                    {
                        PredeterminarVolumen[Cara - 1] = false;
                        PredeterminarValor[Cara - 1] = false;
                    }
                    //Valor de programación de la cara
                    //ValorPredeterminado[Cara - 1] = Convert.ToDouble(ValorProgramado);
                    //Tipo de programación (m3 o $)
                    //PredeterminarVolumen[Cara - 1] = false;// Convert.ToDouble(TipoProgramacion);
                }
                //else
                //{
                //    SW.WriteLine(DateTime.Now + "  Cara " + Cara + " fuera de red de surtidores. Método: oEvento_VentaAutorizada");
                //    SW.Flush();
                //}
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_VentaAutorizada: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEvento_TurnoAbierto( string Surtidores,  string PuertoTerminal,  string Precio)
        {
            try
            {
                IniciaTomaLecturasTurno(Surtidores, true);  //Indica que las lecturas a tomar son las iniciales 
                PrecioEDS = Convert.ToDouble(Precio);  //Asigna el nuevo precio
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEvento_TurnoCerrado( string Surtidores,  string PuertoTerminal)
        {
            try
            {
                IniciaTomaLecturasTurno(Surtidores, false); //Indica que las lecturas a tomar son las finales      
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEvento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEventos_InactivarCaraCambioTarjeta( byte Cara,  string Puerto)
        {
            try
            {
                InactivarCara[Cara - 1] = true;
                this.Puerto[Cara - 1] = Puerto;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Inactivacion");
                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEventos_InactivarCaraCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEventos_FinalizarCambioTarjeta( byte Cara)
        {
            try
            {
                ActivarCara[Cara - 1] = true;
                //CaraActiva[Cara - 1] = true;
                EstructuraRedSurtidor[Cara - 1].Activa = true;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Activacion");
                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEventos_FinalizarCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEventos_FinalizarVentaPorMonitoreoCHIP( byte cara)
        {
            try
            {
                this.DetenerVentaCara[cara-1] = true;

                SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(cara) + "|Evento|Recibe orden de detenci{on");
                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEventos_FinalizarVentaPorMonitoreoCHIP: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        
        }
        #endregion
    }
}