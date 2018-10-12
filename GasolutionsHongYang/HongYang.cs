
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;
using POSstation.Protocolos;
using System.Net.Sockets;
using System.Net;
using System.Globalization;



namespace POSstation.Protocolos
{
    public class HongYang : iProtocolo
    {
        #region EventosDeProtocolo

        private bool AplicaServicioWindows = false;

        public event iProtocolo.CambioMangueraEnVentaGerenciadaEventHandler CambioMangueraEnVentaGerenciada;//listo

        public event iProtocolo.CaraEnReposoEventHandler CaraEnReposo;//--listo

        public event iProtocolo.VentaFinalizadaEventHandler VentaFinalizada;//--Listo

        public event iProtocolo.LecturaTurnoCerradoEventHandler LecturaTurnoCerrado;//--

        public event iProtocolo.LecturaTurnoAbiertoEventHandler LecturaTurnoAbierto;//--//LIsto

        public event iProtocolo.LecturaInicialVentaEventHandler LecturaInicialVenta;//--listo

        public event iProtocolo.VentaParcialEventHandler VentaParcial;//listo

        public event iProtocolo.CambioPrecioFallidoEventHandler CambioPrecioFallido;//listo

        public event iProtocolo.CancelarProcesarTurnoEventHandler CancelarProcesarTurno;//--Listo

        public event iProtocolo.ExcepcionOcurridaEventHandler ExcepcionOcurrida;//--

        public event iProtocolo.VentaInterrumpidaEnCeroEventHandler VentaInterrumpidaEnCero;//Listo

        public event iProtocolo.AutorizacionRequeridaEventHandler AutorizacionRequerida;//--Listo

        public event iProtocolo.IniciarCambioTarjetaEventHandler IniciarCambioTarjeta;//no

        public event iProtocolo.LecturasCambioTarjetaEventHandler LecturasCambioTarjeta;//no

        public event iProtocolo.NotificarCambioPrecioMangueraEventHandler NotificarCambioPrecioManguera;//

        #endregion

        #region DECLARACION DE VARIABLES Y DEFINICIONES

        Dictionary<byte, RedSurtidor> EstructuraRedSurtidor;        //Diccionario donde se almacenan las Caras y sus propiedades

        public enum ComandoSurtidor
        {
            ObtenerEstado = 0X12,
            Borra_Totalizador_Turno = 0x04,
            ObtenerPrecio = 0x8C,
            ObtenerDespacho = 0x8F,
            ObtenerTotalizador = 0x8E,
            obtenerTotalizador_Reestablecido = 0x8D, 
            EstablecerPrecio = 0x80,
            AutorizarDespacho = 0x08,
            Autorizar_PreVOl = 0x8B,
            Autorizar_PreImpor = 0x89,
            DetenerSurtidor = 0x10,
            BloquearSurtidor = 0x15,
            DesbloquearSurtidor = 0x14

          
           
        }   //Define los posibles COMANDOS que se envian al Surtidor
       
        ComandoSurtidor ComandoCaras;


        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
        byte CaraID;
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int BytesEsperados;             //Declara la cantidad de bytes esperados por Comando
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO
        bool TramaEco;                  //Bandera que indica si dentro de la trama respuesta viene eco o no

        // ******* DCF *******


        byte ID1;  //A0
        byte ID2; //B0
        int ContaID;
        bool ActivaID;

        //int NumMangueraCara; //Cantidiad de mangueras por cara 

       

        string strPrecio;
        string strValorImporte;
        string strValorVolumen;



        /*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        bool FalloComunicacion;      //Almacena el tipo de fallo de comunicacion        

        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR   
        
        byte[] TramaTemporal = new byte[1];


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
        bool CondicionCiclo2 = true;
        bool EncuentaFinalizada = false;

        byte CaraTmp; // Utilizado para las caras con alias mas de 16 caras

    



        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        string Puerto;

        TcpClient ClienteHongYang;
        NetworkStream Stream;
        AsyncCallback callBack = new AsyncCallback(CallBackMethod);


        int Conta_Finventa = 0; 

        #endregion


        public HongYang(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
                ActivaID = true; // DCF
                this.AplicaServicioWindows = true;

                this.Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "-HongYang-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "-HongYang-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2014.12.03 0412");//Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2015.02.26 1818");//Fin de venta sin lectiuras de totales iguales 
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2016.06.28 1217");//Loguear todo lo que llegeue 28/06/2016 1217
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2016.08.26 0252");//Time Out 100
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2018.03.8 1755");//DCF Archivos .txt 08/03/2018 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2018.07.09 1508 ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF 09/07/2018 
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2018.10.05 1614. ");//
                
                SWRegistro.Flush();

                
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 2400;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.None;
                    PuertoCom.ReadBufferSize = 4096;
                    PuertoCom.WriteBufferSize = 4096;
                    PuertoCom.Open();
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }

                //EstructuraRedSurtidor es la referencia con la que se va a trabajar
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                {
                    {
                        foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                            SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                                " - IdProducto: a" + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1);
                    }

                    //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                    //esto funciona siempre y cuando no se hagan mas venta en la cara. 
                    if (Convert.ToInt32(oCara.GradoMangueraVentaParcial) >= 0)
                    {
                        SWRegistro.WriteLine("*********************************** *************************************************** ");
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Recuperar Venta para el Grado: " + oCara.GradoMangueraVentaParcial + " - Venta Parcial: " + oCara.EsVentaParcial);
                        SWRegistro.WriteLine("*********************************** *************************************************** ");
                    }
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
                string MensajeExcepcion = "Excepción en el Constructor de la Clase HongYang";
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }


        public HongYang(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
                ActivaID = true; // DCF
                this.AplicaServicioWindows = true;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-HongYang-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-HongYang-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);


                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2014.12.03 0412");//Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2015.02.26 1818");//Fin de venta sin lectiuras de totales iguales 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2016.06.28 1217");//Loguear todo lo que llegeue 28/06/2016 1217
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2016.08.26 1442");//Time Out 100
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2018.03.8 1755");//DCF Archivos .txt 08/03/2018 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2018.07.09 1508 ");//Utilizado para solicitud de lecturas por surtidor - Manguera DCF 09/07/2018 
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2018.10.05 1614..");//
                SWRegistro.Flush();

                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Puerto;

                AplicaServicioWindows = true;
               
                if (EsTCPIP)
                {
                    try
                    {
                        //Crea y abre la conexión con el Servidor
                        ClienteHongYang = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClienteHongYang.GetStream();

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }


                }
                else  if (!PuertoCom.IsOpen)
                    {
                        PuertoCom.PortName = Puerto;
                        PuertoCom.BaudRate = 2400;
                        PuertoCom.DataBits = 8;
                        PuertoCom.StopBits = StopBits.One;
                        PuertoCom.Parity = Parity.None;
                        PuertoCom.ReadBufferSize = 4096;
                        PuertoCom.WriteBufferSize = 4096;
                        try
                        {
                            PuertoCom.Open();
                        }
                        catch (Exception Excepcion)
                        {
                            string MensajeExcepcion = "No se pudo abrir puerto de comunicación _ Configuración TCPIP recibida: " + Excepcion;
                            SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                            SWRegistro.Flush();
                            throw Excepcion; //throw new Exception ("Comunicacion con surtidor no disponible");
                        }
                        PuertoCom.DiscardInBuffer();
                        PuertoCom.DiscardOutBuffer();
                    }


                ////Escribe encabezado en archivo de Inconsistencias
                //SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_TC/IP. Modificado 2014.12.03 0412");//Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo HongYang_Mixto. Modificado 2015.02.26 1818");//Fin de venta sin lectiuras de totales iguales 
              
                //SWRegistro.Flush();



                //EstructuraRedSurtidor es la referencia con la que se va a trabajar
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)//recoro las caras 
                {
                    {
                        foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)//recorro los grados
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                                " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1);

                        
                           if(  EstructuraRedSurtidor[oCara.Cara].MultiplicadorPrecioVenta == 10)
                            {
                             
                                EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel1 =
                                      EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel1/
                                EstructuraRedSurtidor[oCara.Cara].MultiplicadorPrecioVenta;

                                EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel2 =
                                   EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel2 /
                                EstructuraRedSurtidor[oCara.Cara].MultiplicadorPrecioVenta;


                                SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Evento|EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel1 = " +
                                    EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel1);
                                SWRegistro.Flush();

                                SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Evento|EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel2 = " +
                               EstructuraRedSurtidor[oCara.Cara].ListaGrados[oGrado.NoGrado].PrecioNivel2);
                                SWRegistro.Flush();

                            }
                        }
                    }

                    //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                    //esto funciona siempre y cuando no se hagan mas venta en la cara. 
                    if (Convert.ToInt32(oCara.GradoMangueraVentaParcial) >= 0)
                    {
                        SWRegistro.WriteLine("*********************************** *************************************************** ");
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Recuperar Venta para el Grado: " + oCara.GradoMangueraVentaParcial + " - Venta Parcial: " + oCara.EsVentaParcial);
                        SWRegistro.WriteLine("*********************************** *************************************************** ");
                    }
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
                string MensajeExcepcion = "Excepción en el Constructor de la Clase HongYang";
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }



        private byte ConvertirCaraBD(byte caraBD) //YEZID Alias de las caras //DCF
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

                //para loguear los factores
                foreach (RedSurtidor ORedCaras2 in EstructuraRedSurtidor.Values)
                {
                    byte CaraEncuestada2 = ORedCaras2.Cara;

                    //if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)-- 06/07/2012
                    if (EstructuraRedSurtidor[CaraEncuestada2].MultiplicadorPrecioVenta == 0)
                    {
                        EstructuraRedSurtidor[CaraEncuestada2].MultiplicadorPrecioVenta = 1;
                    }

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada2 + "|FactorVolumen: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorVolumen)
                           + " - FactorTotalizador: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorTotalizador)
                           + " - FactorImporte: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorImporte)
                           + " - FactorPrecio: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorPrecio)
                           + " - MultiplicadorPrecioVenta: " + EstructuraRedSurtidor[CaraEncuestada2].MultiplicadorPrecioVenta);
                    SWRegistro.Flush();
                }

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Inicia ciclo de encuesta a " + EstructuraRedSurtidor.Count + " caras");
                SWRegistro.Flush();

                //Ciclo Infinito
                while (CondicionCiclo)
                {
                    VerifySizeFile();
                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                    {
                        if (CondicionCiclo2)
                        {
                            //Si la cara está activa, realizar proceso de encuesta
                            if (ORedCaras.Activa == true)
                            {
                                CaraEncuestada = ORedCaras.Cara;
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                                EncuentaFinalizada = false;

                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerEstado))
                                    TomarAccion();

                                EncuentaFinalizada = true;
                            }
                            Application.DoEvents();
                            Thread.Sleep(20);
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {

                EncuentaFinalizada = true;
                CondicionCiclo2 = true;

                string MensajeExcepcion = "Excepcion en el Metodo CicloCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
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
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "-HongYang-Tramas.(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)               
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhh") + "-HongYang-Sucesos(" + Puerto + ").txt";
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
        private bool   ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                ComandoCaras = ComandoaEnviar;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 3;
                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;
                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

               

                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {
                    ArmarTramaTx();

                    if (EsTCPIP)
                        EnviarComando_TCPIP();

                    else
                        EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor
                    //if (BytesEsperados > 0)
                    //{
                    if (EsTCPIP)
                        RecibirInformacion_TCPIP();
                    else
                        RecibirInformacion();

                    Reintentos += 1;
                    //}
                } while (FalloComunicacion == true && Reintentos < MaximoReintento);

                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion)
                {
                    // Activa el envio de los comando A  y B5 de new por falla de comunicacion
                    //////////EstructuraRedSurtidor[CaraEncuestada].ComandoFX_A5_B5 = false;

                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {

                        CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                        string MensajeErrorLectura = "Error en Comunicacion con Surtidor";
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            bool EstadoTurno = false;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;

                            if (AplicaServicioWindows)
                            {
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                            }

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            bool EstadoTurno = true;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                            if (AplicaServicioWindows)
                            {
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                            }

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                    }

                    if (!EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Perdida de comunicacion en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = true;
                    }

                    //Regresa el parámetro FALSE si hubo error en la trama o en la comunicación con el surtidor
                    return false;
                }
                else
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Se reestablece comunciación con surtidor en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de el método ProcesoEnvioComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        private void ArmarTramaTx()
        {
            try
            {

                switch (ComandoCaras)
                {
                    #region Establecer Precio ***                
                    case ComandoSurtidor.EstablecerPrecio:

                    TimeOut = 100; 
                    BytesEsperados = 3;

                       TramaTx = new byte[8] { CaraEncuestada, 0x07, (byte)(ComandoCaras), 0x00, 0x00, 0x00,0x00, 0x00};

                       
                        strPrecio = Convert.ToInt32(Convert.ToDecimal(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1) *
                                                      EstructuraRedSurtidor[CaraEncuestada].FactorPrecio).ToString("X2").PadLeft(8, '0');


                             TramaTx[3] = Convert.ToByte(strPrecio.Substring(0, 2), 16);
                             TramaTx[4] = Convert.ToByte(strPrecio.Substring(2, 2), 16);
                             TramaTx[5] = Convert.ToByte(strPrecio.Substring(4, 2), 16);
                             TramaTx[6] = Convert.ToByte(strPrecio.Substring(6, 2), 16);




                        break;
                    #endregion

                    #region Borra Totalizador para Turno ****

                    case ComandoSurtidor.Borra_Totalizador_Turno:
                        TimeOut = 100; 
                        BytesEsperados = 3;

                       TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00};

                       break;

                    #endregion

                    #region Autorizar Despacho *****
                    case ComandoSurtidor.AutorizarDespacho:
                       TimeOut = 100;
                       BytesEsperados = 3;

                       TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                       break;

                    #endregion

                    #region Predeterminar Importe/Volumen y Autorizar ******
                    case ComandoSurtidor.Autorizar_PreImpor:
                    case ComandoSurtidor.Autorizar_PreVOl:
                   

                       TimeOut = 100;
                       BytesEsperados = 3;
                       string Valor_programado = "0";
                       string Cantidad;

                      

                       if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                       {
                           Valor_programado = Convert.ToString(Convert.ToInt32((EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado *
                          EstructuraRedSurtidor[CaraEncuestada].FactorImporte) / EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta)).PadLeft(8, '0');

                           ComandoCaras = ComandoSurtidor.Autorizar_PreImpor;
                       
                       }
                       else if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen)
                       {
                           Valor_programado = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado *
                           EstructuraRedSurtidor[CaraEncuestada].FactorVolumen)).PadLeft(8, '0');
                           ComandoCaras = ComandoSurtidor.Autorizar_PreVOl;
                       }
                                
                     TramaTx = new byte[8] { CaraEncuestada, 0x07, (byte)ComandoCaras, 0x00, 0x00, 0x00, 0x00, 0x00 };

                      Cantidad = Convert.ToInt32(Convert.ToDecimal(Valor_programado)).ToString("X2").PadLeft(8, '0');

                      TramaTx[3] = Convert.ToByte(Cantidad.Substring(0, 2), 16);
                      TramaTx[4] = Convert.ToByte(Cantidad.Substring(2, 2), 16);
                      TramaTx[5] = Convert.ToByte(Cantidad.Substring(4, 2), 16);
                      TramaTx[6] = Convert.ToByte(Cantidad.Substring(6, 2), 16);

                    break;
                    #endregion

                    #region Obtener Precio de venta ********

                    case ComandoSurtidor.ObtenerPrecio:
                    TimeOut = 100;
                    BytesEsperados = 8;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion

                    #region Obtener Total Reestablecido ********

                    case ComandoSurtidor.obtenerTotalizador_Reestablecido:
                    TimeOut = 100;
                    BytesEsperados = 15;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion

                    #region Obtener Totalizador ********

                    case ComandoSurtidor.ObtenerTotalizador:
                    TimeOut = 100;
                    BytesEsperados = 15;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion

                    #region Obtener Datos de Despacho ********

                    case ComandoSurtidor.ObtenerDespacho:
                    TimeOut = 100;
                    BytesEsperados = 13;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion

                    #region Detener Despacho Stop ********

                    case ComandoSurtidor.DetenerSurtidor:
                    TimeOut = 100;
                    BytesEsperados = 3;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion

                    #region Bloquear Surtidor Lock********

                    case ComandoSurtidor.BloquearSurtidor:
                    TimeOut = 100;
                    BytesEsperados = 3;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion
                        
                    #region Desbloquear Surtidor UnLock********

                    case ComandoSurtidor.DesbloquearSurtidor:
                    TimeOut = 100;
                    BytesEsperados = 3;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion

                    #region Obtener Estado********

                    case ComandoSurtidor.ObtenerEstado:
                    TimeOut = 100;
                    BytesEsperados = 10;

                    TramaTx = new byte[4] { CaraEncuestada, 0x03, (byte)(ComandoCaras), 0x00 };

                    break;

                    #endregion




                }

                //Calcula los bytes Checck
                TramaTx[TramaTx.Length - 1] = Calcular_checksum(TramaTx, TramaTx.Length);


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de el método ArmarTramaTx";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private byte Calcular_checksum(byte[] datos, int longitud)
        {
            byte[] res = new byte[1] { 0x00 };

            long suma = 0;

            for (int j = 0; longitud > j; j++)
            {
                suma += Convert.ToInt16(datos[j].ToString(), 10);
            }

            suma = 256 - suma;
            res = BitConverter.GetBytes(suma);
            return res[0];
        }


        private void cambiar_pariedad(System.IO.Ports.Parity paridad)
        {
            PuertoCom.Parity = paridad;
        }

        public static double GetDouble(string number)
        {double value = 0;
            try
            {
            
            number = number.Trim();
            if (String.IsNullOrEmpty(number)) number = "0";

             string currencyDecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            number = number.Replace(number.Contains(",") ? "," : ".", currencyDecimalSeparator);

            
                value = Convert.ToDouble(number);
                if (Double.IsInfinity(value)) value = 0;
            }
            catch { value = -1; }

            return value;
        }



        //ENVIA EL COMANDO AL SURTIDOR
        private void EnviarComando()
        {
            try
            {
                //Limpia todo lo que este en el Buffer de salida y Buffer de entrada del puerto
                PuertoCom.DiscardOutBuffer();
                PuertoCom.DiscardInBuffer();

                cambiar_pariedad(System.IO.Ports.Parity.None);
                PuertoCom.Write(TramaTx, 0, 1);

                System.Threading.Thread.Sleep(10);

                cambiar_pariedad(System.IO.Ports.Parity.Space);
                PuertoCom.Write(TramaTx, 1, TramaTx.Length - 1);


                //Escribe en el puerto el comando a Enviar.
                //PuertoCom.Write(TramaTx, 0, TramaTx.Length);

                /////////////////////////////////////////////////////////////////////////////////
                //LOGUEO DE TRAMA TRANSMITIDA
                string strTrama = "";
                for (int i = 0; i <= TramaTx.Length - 1; i++)
                    strTrama += TramaTx[i].ToString("X2") + "|";


                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF
                SWTramas.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                    "|" + CaraID + "|Tx|" + ComandoCaras + "|" + strTrama);

                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////

                //Tiempo muerto mientras el Surtidor Responde
                Thread.Sleep(TimeOut);
                Thread.Sleep(100);// prueba con tarjeta nueva 
             
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase EnviarComando";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
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
                        //Stream.Write(TramaTx, 0, TramaTx.Length);
                        //Stream.Flush();


                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:   Sockets.SocketException ");
                        SWTramas.Flush();

                    }
                    catch (Exception)
                    {
                        VerificarConexion();
                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesEsperados.ToString());
                        SWTramas.Flush();

                    }
                }
                catch (System.IO.IOException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        //Stream.Write(TramaTx, 0, TramaTx.Length);
                        //Stream.Flush();


                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:   VerificarConexion ");
                        SWTramas.Flush();

                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesEsperados.ToString());
                        SWTramas.Flush();

                    }

                }
                catch (System.Exception)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        //Stream.Write(TramaTx, 0, TramaTx.Length);
                        //Stream.Flush();


                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesEsperados.ToString());
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
               
                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
               
                    //Definicion de Trama Temporal
                   TramaTemporal = new byte[Bytes];

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
                         "|" + CaraID + "|Rx|" + ComandoCaras + "|" + strTrama + " # " + TramaRx.Length ); //Loguear todo lo que llegeue 28/06/2016 1217

                    SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////


                    if (Bytes >= BytesEsperados)
                    {
                    //Revisa si existe problemas en la trama
                    if (ComprobarIntegridadTrama())
                             AnalizarTrama();
                    else
                    {
                        FalloComunicacion = true;

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Comando " + ComandoCaras + ". Bytes con daño en integridad de trama");
                        SWRegistro.Flush();
                    }
                }

                else if (FalloComunicacion == false)
                {
                    FalloComunicacion = true;
                    if (!EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + ComandoCaras + ". Bytes Esperados: " + BytesEsperados + " - Bytes Recibidos: " + Bytes);
                        SWRegistro.Flush();
                    }
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo RecibirInformacion";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }

        }

        //REVISA LA INTEGRIDAD DE LA TRAMA
        private bool ComprobarIntegridadTrama()
        {
            try
            {

             byte chechk1 = Calcular_checksum(TramaTemporal, TramaTemporal.Length-1); //Check


                  if (chechk1 != TramaTemporal[TramaTemporal.Length-1])
                  {
                       return false;
                  }

                  else
                    return true;              
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo ComprobarIntegridadTrama";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }



        public void VerificarConexion()
        {
            int iReintento = 0;
            string Comando = "";
            try
            {
                if (ClienteHongYang == null)
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
                            ClienteHongYang = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                            SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*9|Verificando conexion 3" + EsInicializado);

                            SWTramas.Flush();

                            if (ClienteHongYang == null)
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

                        if (ClienteHongYang != null)
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
                if (!this.ClienteHongYang.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClienteHongYang.Client.BeginDisconnect(true, callBack, ClienteHongYang);

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



                while (!this.ClienteHongYang.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteHongYang.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteHongYang);
                        //ClienteHongYang.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteHongYang.Client.Connected)
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
                this.Stream = ClienteHongYang.GetStream();
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
                ClienteHongYang.Close();
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
                ClienteHongYang = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteHongYang.GetStream();
                if (this.ClienteHongYang.Client.Connected == true)
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

        public void RecibirInformacion_TCPIP()
        {
            try
            {
                int Bytes =0;

               if (!TramaEco)
                    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                BytesEsperados = BytesEsperados + eco;


                if (Stream==null)
                {
                    FalloComunicacion = true;
                    return;
                }

                if (!Stream.DataAvailable)
                    Thread.Sleep(40);



                if (Stream.DataAvailable)
                {
                    byte[] TramaRxTemporal = new byte[BytesEsperados];

                    // int Bytes = PuertoCom.BytesToRead;
                    if (Stream.CanRead)
                    {
                        do
                        {
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            Bytes = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);


                        } while (Stream.DataAvailable);
                    }
                    
                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    if (Bytes >= BytesEsperados)
                    {
                        //Definicion de Trama Temporal
                        byte[] TramaTemporal = new byte[Bytes];

                        ////Almacena informacion en la Trama Temporal para luego eliminarle el eco
                        //PuertoCom.Read(TramaTemporal, 0, Bytes);
                        //PuertoCom.DiscardInBuffer();

                        //Se dimensiona la Trama a evaluarse (TramaRx)
                        TramaRx = new byte[TramaTemporal.Length - eco];

                        //Almacena los datos reales (sin eco) en TramaRx
                        for (int i = 0; i <= (TramaTemporal.Length - eco - 1); i++)
                            TramaRx[i] = TramaRxTemporal[i + eco];

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
                            AnalizarTrama();

                            FalloComunicacion = false;

                        }
                        else
                        {
                            FalloComunicacion = true;

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Comando " + ComandoCaras + ". Bytes con daño en integridad de trama");
                            SWRegistro.Flush();
                        }
                    }
                    else if (FalloComunicacion == false) //Faltaba  FalloComunicacion = true;..... DCF 19-11-2014
                    {
                        FalloComunicacion = true;

                        if (!EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + ComandoCaras + ". Bytes Esperados : " + BytesEsperados + " - Es Menor que  - Bytes Recibidos: " + Bytes);
                            SWRegistro.Flush();
                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespacho ||
                        EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado)
                        {
                            Thread.Sleep(200);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + ComandoCaras + " - Tiempo de Espera de 200 ms");
                            SWRegistro.Flush();
                        }
                        else
                         Thread.Sleep(10);
                    }
                }
               else if (FalloComunicacion == false)
                  {
                        FalloComunicacion = true;
                        if (!EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + ComandoCaras + ". Bytes Esperados: " + BytesEsperados + " - Bytes Recibidos: " + Bytes);
                            SWRegistro.Flush();
                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespacho ||
                        EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado)
                        {
                            Thread.Sleep(200);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + ComandoCaras + " - Tiempo de Espera de 200 ms");
                            SWRegistro.Flush();
                        }
                        else
                            Thread.Sleep(10);
                    }

                    // ************* DCF **********************
                    // Detectar el no recibo de comando y asignación  del  nuevo comando.
                    // ************* DCF **********************


                    if (BytesEsperados != Bytes) // (Bytes == 0)// Envia un Nuevo Comando en caso que el surtidor no responde al comando enviado.
                    {
                        switch (ComandoCaras)
                        {
                            case (ComandoSurtidor.EstablecerPrecio): //Para enviar el nuevo comando CX en el cambio de precio para suertidores 4 precios 22/07/2013

                                //ComandoCaras = ComandoSurtidor.EstablecerPrecio_CX;

                             
                                FalloComunicacion = false;

                                if (ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|EstablecerPrecio Con comando _CX");
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|No respondio EstablecerPrecio comando _CX");
                                    SWRegistro.Flush();
                                }

                                //ArmarTramaTx();

                                break;
                        }
                    
                    }                  
            }

            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo RecibirInformacion";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }


        }
               

        public void LimpiarSockets()
        {
            try
            {
                //ClienteHongYang.Client.Disconnect(false);  
                ClienteHongYang.Client.Close();
                ClienteHongYang.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteHongYang = null;
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|LimpiarSockets:" + ex.Message);
                SWRegistro.Flush();

            }

        }

        public static void CallBackMethod(IAsyncResult asyncresult)
        {

        }




        #endregion

        #region ANALISIS DE TRAMAS Y RECONSTRUCCIÓN DE DATOS PROVENIENTE DEL SURTIDOR

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void  AnalizarTrama()
        {
            try
            {
                switch (ComandoCaras)
                {
                    case ComandoSurtidor.AutorizarDespacho:               
                    case ComandoSurtidor.ObtenerEstado:
                    case ComandoSurtidor.DetenerSurtidor:
                        RecuperarEstado();
                        break;
                    case ComandoSurtidor.ObtenerDespacho:

                         decimal Importe =
                             Convert.ToDecimal(Convert.ToInt64(TramaTemporal[2].ToString("X2") + TramaTemporal[3].ToString("X2") + TramaTemporal[4].ToString("X2") + TramaTemporal[5].ToString("X2"), 16));
                        
                        decimal Volumen =
                           Convert.ToDecimal(Convert.ToInt64(TramaTemporal[6].ToString("X2") + TramaTemporal[7].ToString("X2") + TramaTemporal[8].ToString("X2") + TramaTemporal[9].ToString("X2"), 16));

                
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta = (Importe / EstructuraRedSurtidor[CaraEncuestada].FactorImporte)
                                                                            * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;

                        EstructuraRedSurtidor[CaraEncuestada].Volumen = Volumen / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;



                        break;


                   
                    case ComandoSurtidor.ObtenerTotalizador:
                        int IndiceTramRx = 0;

                         //Totalizador para la cara con una sola Manguera. IndiceTramRx + 6
                            foreach (Grados oGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                            {

                                decimal Total =  Convert.ToDecimal(TramaTemporal[7].ToString("X2") +  TramaTemporal[6].ToString("X2") + 
                                                TramaTemporal[5].ToString("X2") + TramaTemporal[4].ToString("X2") + 
                                                TramaTemporal[3].ToString("X2") +  TramaTemporal[2].ToString("X2"));

                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].Lectura =
                                    Total / EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador;

                            }
                        break;

                    case ComandoSurtidor.ObtenerPrecio:

                         decimal PrecioSurtidorNivel1 =
                            Convert.ToDecimal(Convert.ToInt32(TramaTemporal[2].ToString("X2") + TramaTemporal[3].ToString("X2") + TramaTemporal[4].ToString("X2") + TramaTemporal[5].ToString("X2"), 16));



                         EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = (PrecioSurtidorNivel1 / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)
                                                                            * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;

                         SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio entregado por el surtidor. PV = " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                            SWRegistro.Flush();

               
                        break;

                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo AnalizarTrama";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void RecuperarEstado()
        {
            try
            {
                int CodigoEstado = TramaRx[1];

                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;


                     byte satate = TramaRx[1];
                      int cont = 0;

                        List<int> bits = new List<int>();
                        do
                        {
                            bits.Add(satate % 2); satate /= 2;
                            cont++;
                        }
                        while (satate > 0 || cont == 7);

                      


                      if(bits[5]==0)//reporso Bit 5 32d = 0x20 
                      {
                            if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespacho;
                                //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                                //esto funciona siempre y cuando no se hagan mas venta en la cara. 
                                if (Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].GradoMangueraVentaParcial) >= 0)
                                {
                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].GradoMangueraVentaParcial);
                                    EstructuraRedSurtidor[CaraEncuestada].GradoMangueraVentaParcial = "-1";
                                }
                            }
                            else
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Espera;   
                
                      }
                      else // bits[5]= 1 //Manguera levantada por autorizar
                      {
                            if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Fin de despacho forzado en Por Autorizar");
                                SWRegistro.Flush();
                            }
                            else
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorAutorizar;
                      }


                      if (bits[3] == 1) //1 Despacho - 0 Fin de Despacho
                      {
                          EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;
                          // return;
                      }
                      else
                      {
                          if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                          {
                              EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespacho;
                              //return;
                          }
                      }


                
               /*


                    case (0xD0):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangDespachoD0;
                        break;
                    case (0xF0):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangDespachoF0;
                        break;
                    case (0x94):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangDespacho94;
                        break;
                    case (0xD4):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangDespachoD4;
                        break;
                    case (0x91):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangFinDespacho91;
                        break;
                    case (0x95):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangFinDespacho95;
                        break;
                    case (0x98):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangVentaDetenida98;
                        break;
                    case (0x9C):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangVentaDetenida9C;
                        break;
                    case (0x99):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangFinDespacho99;
                        break;
                    case (0x9D):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.HongYangFinDespacho9D;
                        break;
                    default:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Estado Indeterminado: " + CodigoEstado.ToString("X2"));
                        SWRegistro.Flush();
                        break;
                }
                */
                SWRegistro.Flush();

                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo AsignarEstado";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //CONSTRUYE LOS VALORES OBTENIDOS EN LA TRAMA DE RECEPCIÓN PROVENIENTE DEL SURTIDOR
        private decimal ObtenerValor(int PosicionInicial, int PosicionFinal)
        {
            try
            {
                decimal ValorObtenido = new decimal();

                string strValorEnTrama = "";
                for (int i = PosicionFinal; i >= PosicionInicial; i -= 2)
                    strValorEnTrama += TramaRx[i].ToString("X2");

                ValorObtenido = Convert.ToDecimal(strValorEnTrama);

                return ValorObtenido;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo ObtenerValor";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
                return 0;
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
                switch (EstructuraRedSurtidor[CaraEncuestada].Estado)
                {

                    case (EstadoCara.Espera):
                        //Informa cambio de estado

                        //Reset del elemento que indica que la Cara debe ser autorizada 23/09/2011
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;


                        CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            int mangueraColgada = -1;///// OJO /////                           

                            if (AplicaServicioWindows)
                            {
                                if (CaraEnReposo != null)
                                {
                                    CaraEnReposo(CaraID, mangueraColgada);
                                }
                            }

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso| Cara en Espera");
                            SWRegistro.Flush();
                        }

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia toma de lecturas para cierre o apertura");
                            SWRegistro.Flush();
                            LecturaAperturaCierre();
                        }

                        //ORedSurtidor.CambiarProductoAMangueras = true;
                        //Revisa si se tiene que hacer cambio de producto en alguna manguera de la cara
                        if (EstructuraRedSurtidor[CaraEncuestada].CambiarProductoAMangueras)// New DCF
                        {
                            //Revisando en que grados hay que cambiar el producto
                            foreach (Grados OGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                            {
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                                //Si hay cambio de producto se realiza el cambio de precio
                                if (OGrado.CambiarProducto)
                                {
                                    //Si se aplica satisfactoriamente el cambio de precio se hace efectivo el cambio de producto
                                    if (CambiarPreciosEnGrado(OGrado))
                                    {
                                        int MangueraANotificar = OGrado.MangueraBD;
                                        OGrado.Autorizar = true;
                                        OGrado.IdProducto = OGrado.IdProductoACambiar;


                                        if (AplicaServicioWindows)
                                        {
                                            if (NotificarCambioPrecioManguera != null)
                                            {
                                                NotificarCambioPrecioManguera(MangueraANotificar);
                                            }
                                        }

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

                            EstructuraRedSurtidor[CaraEncuestada].CambiarProductoAMangueras = false;
                        }


                        break;

                    case (EstadoCara.PorAutorizar):
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error

                        CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {

                            string MensajeErrorLectura = "Manguera descolgada";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado &&
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)

                        {
                             if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                                  {
                                            EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = 0;
                                            int IdProducto =
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto;
                                            int IdManguera =
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;
                                            string Lectura =
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura.ToString("N3");

                                            byte CaraTemp = CaraID; //DCF


                                            if (AplicaServicioWindows)
                                            {
                                                if (AutorizacionRequerida != null)
                                                {
                                                    AutorizacionRequerida(CaraID, IdProducto, IdManguera, Lectura,"");
                                                }
                                            }

                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraTemp + "|Evento|Informa requerimiento de autorizacion. Grado: "
                                                + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + " - Producto: " +
                                                IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura);
                                            SWRegistro.Flush();
                                        }
                                        else
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondio comando de obtener Totalizador para Lectura Inicial Venta");
                                            SWRegistro.Flush();
                                        }                                    
                                }


                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                        {
                            CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura;

                            string strLecturasVolumen =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta.ToString("N3");

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

                            int Reintenos = 1;
                            do
                            {
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF



                                if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                                {
                                    ProcesoEnvioComando(ComandoSurtidor.Autorizar_PreImpor);
                                }
                                else if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen)
                                {
                                    ProcesoEnvioComando(ComandoSurtidor.Autorizar_PreVOl);
                                }
                                else if (!ProcesoEnvioComando(ComandoSurtidor.AutorizarDespacho))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondió comando de Autorizar Despacho");
                                    SWRegistro.Flush();
                                }




                                //se consulta el estado para conocer si se inicio el despacho 
                                Thread.Sleep(50);
                                ProcesoEnvioComando(ComandoSurtidor.ObtenerEstado);
                                Reintenos++;
                            } while ((EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.PorAutorizar ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera) &&
                                Reintenos <= 2);

                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizado ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despachando )
                            {
                                EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Surtidor en Despacho.");
                                SWRegistro.Flush();


                                break;
                            }                       

                        }

                        break;



                    case EstadoCara.Autorizado:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho

                        CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara Autorizada";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
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
                        break;

                    case EstadoCara.Despachando:
                    case EstadoCara.Despacho:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho

                        //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma deLecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma deLecturas Finales: " + MensajeErrorLectura);
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

                        //Revisa si es necesario detener la venta en curso por Monitoreo de Chip
                        if (EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara)
                        {
                            if (!ProcesoEnvioComando(ComandoSurtidor.DetenerSurtidor))
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No aceptó comando de detención de venta");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Venta detenida");
                                SWRegistro.Flush();
                                EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara = false;
                            }
                        }

                        //Se obtienen los valores de parciales de despacho
                        if (ProcesoEnvioComando(ComandoSurtidor.ObtenerDespacho))
                        {
                            //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                            //Reporta los valores de parciales de despacho                
                            string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                            string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");


                            if (AplicaServicioWindows)
                            {
                                if (VentaParcial != null)
                                {
                                    VentaParcial(CaraID, strTotalVenta, strVolumen);
                                }
                            }

                        }
                        break;

                 

                    case EstadoCara.FinDespacho:
                    case EstadoCara.FinDespachoForzado:

                            CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                            if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                            {
                                string MensajeErrorLectura = "Cara en Fin de Despacho";
                                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                                {
                                    bool EstadoTurno = false;
                                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                    if (AplicaServicioWindows)
                                    {
                                        if (CancelarProcesarTurno != null)
                                        {
                                            CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                        }
                                    }

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                                {
                                    bool EstadoTurno = true;
                                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                    if (AplicaServicioWindows)
                                    {
                                        if (CancelarProcesarTurno != null)
                                        {
                                            CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                        }
                                    }

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
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


                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                        {
                            ProcesoEnvioComando(ComandoSurtidor.ObtenerEstado); //reconfirmar que la venta se termino para evitar dos venta en 1- DCF 21-11-2014 

                            ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador);


                            //decimal Totalizador_Inicial = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta;
                            //decimal Total_actual = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura;


                            ////Fin de venta sin lectiuras de totales iguales 
                          if ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta 
                              < EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura) || (Conta_Finventa > 4))
                            {
                                Conta_Finventa = 0;

                                if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespacho ||
                                    EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado)
                                {
                                    ProcesoFindeVenta();
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|En el Surtidor se Trato de finalizar una venta en Estado : " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                                    SWRegistro.Flush();
                                }

                            }
                            else
                            {
                                Thread.Sleep(100);//espera que el surtidor actualice el totalizador o sus estados 

                                Conta_Finventa++;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error| Datos de Totalizador no actualizado, se realizan Reintentos = " + Conta_Finventa);
                                SWRegistro.Flush(); 
                            }


                           
                        }
                        break;


                    default:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Estado Indeterminado: " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                        SWRegistro.Flush();
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo TomarAccion";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //REALIZA PROCESO DE FIN DE VENTA
        private void ProcesoFindeVenta()
        {
            try
            {
                //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                //Inicializacion de variables
                EstructuraRedSurtidor[CaraEncuestada].Volumen = 0;
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = 0;
                EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = 0;


                if(ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Precio de Venta = " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                    SWRegistro.Flush();   
                }

                if (!ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando de obtencion de totalizadores para Lectura Final de Venta");
                    SWRegistro.Flush();
                }
                else
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaFinalVenta =
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura;

                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerDespacho))
                {

                    //// para importes superiores a 999,999
                    //decimal ImporteCalculado = EstructuraRedSurtidor[CaraEncuestada].Volumen * EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;
                    //string strTotalVenta = "0";

                    //Evalúa si la venta viene en 0

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Datos de Venta: Volumen reportado = " + EstructuraRedSurtidor[CaraEncuestada].Volumen +
                        " | Importe Reportado = " + EstructuraRedSurtidor[CaraEncuestada].TotalVenta);
                        SWRegistro.Flush();


                    if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0 || EstructuraRedSurtidor[CaraEncuestada].TotalVenta != 0)
                    {
                        ////para terpel. Precios de Importe superiores a 999999 se envia el importe calculado Imp = Vol * PV
                        //if (ImporteCalculado > 999999)
                        //{
                        //    strTotalVenta = ImporteCalculado.ToString("N3");

                        //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|ImporteCalculado Venta Superior a 999.999");
                        //    SWRegistro.Flush();
                        //}
                        //else
                        //{
                        //    strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                        //}


                        //Almacena los valores en las variables requerida por el Evento
                        string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                        string strPrecio = EstructuraRedSurtidor[CaraEncuestada].PrecioVenta.ToString("N3");
                        string strLecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaFinalVenta.ToString("N3");
                        string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                        string strLecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta.ToString("N3");
                        string bytProducto = Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto);
                        int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;

                        //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                        //Loguea evento Fin de Venta
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|InformarFinalizacionVenta. Importe: " + strTotalVenta +
                            " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                            " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                        SWRegistro.Flush();



                        //Control de Venta duplicada por Protocolo 21-11-2014 DCF
                        if ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Volumen_Venta_Anterior != EstructuraRedSurtidor[CaraEncuestada].Volumen) ||
                            (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].TotalizadorVolumen_Final != EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaFinalVenta))
                        {

                            String PresionLLenado = "0";
                            if (AplicaServicioWindows)
                            {
                                if (VentaFinalizada != null)
                                {
                                    VentaFinalizada(CaraID, strTotalVenta, strPrecio, strLecturaFinalVenta, strVolumen, bytProducto, IdManguera, PresionLLenado, strLecturaInicialVenta);

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Venta Reportada" );
                                    SWRegistro.Flush();

                                }
                            }

                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error| Surtidor Reporta Venta Duplicada No se envia esta Venta **********************" );
                            SWRegistro.Flush();
                        }

                        //Almacenamiento de la vetan anterior para analizar si existen ventas duplicadas //Control de Venta duplicada por Protocolo 21-11-2014 DCF
                          EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Volumen_Venta_Anterior = EstructuraRedSurtidor[CaraEncuestada].Volumen;
                          EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].TotalizadorVolumen_Final =
                              EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaFinalVenta;


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

                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Venta en CERO");
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando de obtencion de datos de Final de Venta");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo ProcesoFindeVenta";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //PARA TOMAR LECTURAS DE APERTURA Y/O CIERRE DE TURNO
        private void LecturaAperturaCierre()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();

                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                {
                    //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                    System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                    //Cambia el precio si es apertura de turno
                    if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia cambio de precios");
                        SWRegistro.Flush();
                        CambiarPrecios();
                    }

                    foreach (Grados Grado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                    {
                        ArrayLecturas.Add(Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                            Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].Lectura) + "|" +
                            Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioNivel1)); //DCF
                        //Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioSurtidorNivel1));

                        //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Arma Lecturas para turno. Manguera " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].Lectura);
                        SWRegistro.Flush();
                    }

                    System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                    ArrayLecturas.CopyTo(LecturasEnvio);

                    //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                    if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
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
                        EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                    }
                    //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                    if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
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
                        EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondio comando de obtener Totalizador para Lectura Inicial/Final de Turno");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //REALIZA CAMBIO DE PRECIO, DE SER NECESARIO
        private void CambiarPrecios()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio))// cambio para probar en la EDS estrella
                   // if (ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio_CX))
                {
                    if(ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                    {
                        if((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 * EstructuraRedSurtidor[CaraEncuestada].FactorPrecio) ==
                             EstructuraRedSurtidor[CaraEncuestada].PrecioVenta)//obtener precio ??????????????'
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precios aceptados por Surtidor");
                            SWRegistro.Flush();
                        }
                        
                        else
                        {
                          
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Cambio de precio rechazado por Surtidor, Precio del surtidor = " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                            SWRegistro.Flush();
                        }
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondio comando Establecer Precio");
                    SWRegistro.Flush();
                }

                //foreach (Grados Grado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                //{
                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Grado: " + Grado.NoGrado + " - Precio: " +
                //        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioNivel1);
                //    SWRegistro.Flush();
                //}
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CambiarPrecios: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        #endregion


        private bool CambiarPreciosEnGrado(Grados grado)
        {
            bool CambioPrecios = false;

            try
            {
                //Variable que indica qué precio cambiar
                bool PrecioNivel1 = new bool();
                int Reintentos = 0;

                //Si hay cambio de precio pendiente, lo aplica
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].NoGrado == grado.NoGrado)
                    {
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = i;

                        //Obtener el precio Actual de venta en la Car
                        //if (ProcesoEnvioComando(ComandoSurtidor.ObtenerDespacho))
                        //EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 = EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;
                        //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = ObtenerValor(0, 2) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;                   
                        //Compara el Nivel 1 del precio del grado
                        //if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 !=
                        //    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1)
                        //{
                        PrecioNivel1 = true;
                        Reintentos = 0;
                        do
                        {
                            //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                            if (ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio))
                            {
                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 1) //PARA 2 MANGUERAS POR CARA
                                {
                                    if (TramaRx[0] == 0xB0)
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precios aceptados por Surtidor en CambiarPreciosEnGrado");
                                        SWRegistro.Flush();

                                        CambioPrecios = true;
                                        break;
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio en CambiarPreciosEnGrado");
                                        SWRegistro.Flush();
                                    }
                                }


                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count <= 1) //PARA 1 MANGUERAS POR CARA
                                {
                                    CambioPrecios = true;
                                    break;
                                }
                                Reintentos += 1;
                                //if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 ==
                                //    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1)

                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 1: Precio del Surtidor: " +
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 +
                                    " - Precio Requerido: " +
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 +
                                    " - Reintentos: " + Reintentos);
                                SWRegistro.Flush();

                                //REPORTANDO EL CAMBIO DE PRECIO FALLIDO
                                int Manguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD;
                                double Precio = Convert.ToDouble(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1);

                                if (AplicaServicioWindows)
                                {
                                    if (CambioPrecioFallido != null)
                                    {
                                        CambioPrecioFallido(Manguera, Precio);
                                    }
                                }

                            }
                        } while (Reintentos <= 3);
                        //}
                        //else
                        //    CambioPrecios = true;

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




        #region EVENTOS DE LA CLASE

        //private void oEvento_VentaAutorizada(ref byte Cara, ref string Precio, ref string ValorProgramado, ref byte TipoProgramacion, ref string Placa, ref int MangueraProgramada, ref bool EsVentaGerenciada)
        private void oEvento_VentaAutorizada(ref byte Cara, ref string Precio, ref string ValorProgramado, ref byte TipoProgramacion, ref string Placa, ref int MangueraProgramada, ref bool EsVentaGerenciada)
        {
            byte CaraTmp;
            try
            {
                CaraTmp = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {

                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada + " - Gerenciada: " + EsVentaGerenciada);
                    SWRegistro.Flush();


                    //Cara = Convert.ToByte(CaraTmp.ToString());

                    //Bandera que indica que la cara debe autorizarse para despachar
                    EstructuraRedSurtidor[CaraTmp].AutorizarCara = true;

                    //Valor a programar
                    EstructuraRedSurtidor[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    EstructuraRedSurtidor[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

                    //EstructuraRedSurtidor[Cara].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (EstructuraRedSurtidor[CaraTmp].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = true;
                        }
                        else
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = true;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false;
                        }
                    }
                    else
                    {
                        EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;
                        EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor del método oEvento_VentaAutorizada";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_TurnoAbierto(string Surtidores, string PuertoTerminal, System.Array Precios)
        {
            try
            {

                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|*|Evento|Recibido (TurnoAbierto). Surtidores: " + Surtidores);
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
                byte CaraTmp;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores     


                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp) && EstructuraRedSurtidor[CaraTmp].Activa)

                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            //if (EstructuraRedSurtidor[CaraTmp].Activa)
                            //{
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;
                            //}

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1) /
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 23/08/2011;



                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Precio recibido de sistema PrecioNivel1.:  " +
                                     EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1);
                                SWRegistro.Flush();

                                
                            }
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp) && EstructuraRedSurtidor[CaraTmp].Activa)
                        //if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            ////Si la cara esta activa se solicita la toma de lecturas en la apertura
                            //if (EstructuraRedSurtidor[CaraTmp].Activa)
                            //{
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;
                            //}

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1) /
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 23/08/2011;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Precio recibido de sistema PrecioNivel1..:  " +
                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1);
                                SWRegistro.Flush();
                                
                            }
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
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
                byte CaraTmp; //DCF

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaCierreTurno = true;
                            }
                        }

                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }
                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaCierreTurno = true;
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
        {
            byte CaraTmp;
            try
            {

                if (string.IsNullOrEmpty(ValorProgramado))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara.ToString() + "|Evento|Inicio de venta Autorizada | Valor programado NULL" );
                    SWRegistro.Flush();
                    ValorProgramado = "0";
                }
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara.ToString() + "|Evento|Inicio de venta Autorizada " +  ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada + " - Gerenciada: " + EsVentaGerenciada);
                SWRegistro.Flush();
               // string pred = EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado.ToString();

                double Valor_programado = GetDouble(ValorProgramado);

                ValorProgramado = Valor_programado.ToString();

                CaraTmp = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {

                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada + " - Gerenciada: " + EsVentaGerenciada);
                    SWRegistro.Flush();


                    //Cara = Convert.ToByte(CaraTmp.ToString());

                    //Bandera que indica que la cara debe autorizarse para despachar
                    EstructuraRedSurtidor[CaraTmp].AutorizarCara = true;

                    //Valor a programar
                    EstructuraRedSurtidor[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    EstructuraRedSurtidor[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

                    //EstructuraRedSurtidor[Cara].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (EstructuraRedSurtidor[CaraTmp].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = true;
                        }
                        else
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = true;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false;
                        }
                    }
                    else
                    {
                        EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;
                        EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor del método oEvento_VentaAutorizada";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }





        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)
        {
            try
            {

                //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
                //foreach (Manguera OManguera in mangueras)
                //{
                for (int i = 1; i <= mangueras.Count; i++)
                {
                    Manguera OManguera = mangueras.get_Item(i);

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
                string MensajeExcepcion = "Excepcion en el Evento oEventos_ProgramarCambioPrecioKardex:" + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_CerrarProtocolo()
        {

            try
            {

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Recibe evento de detencion de Protocolo");
                SWRegistro.Flush();
                this.CondicionCiclo = false;

                if (PuertoCom.IsOpen)
                {
                    PuertoCom.Close();

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|PuertoCom Close");
                    SWRegistro.Flush();
                }



            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Eventos_CerrarProtocolo " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                throw Excepcion; //throw new Exception ("Comunicacion con surtidor no disponible");
            }



        }

        private void oEventos_CerrarProtocolo()
        {
            //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Recibe evento de detencion de Protocolo");
            SWRegistro.Flush();
            this.CondicionCiclo = false;
        }
        public void Evento_FinalizarCambioTarjeta(byte Cara) { }
        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto)
        {

        }


        public void Evento_FinalizarVentaPorMonitoreoCHIP(byte Cara)
        {
            try
            {
                Cara = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(Cara))
                {
                    EstructuraRedSurtidor[Cara].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(Cara) + "|Evento|Recibe Detencion por Monitoreo de Chip");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEventos_FinalizarVentaPorMonitoreoCHIP: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }
        public void Evento_CancelarVenta(byte Cara)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
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
                    if (EstructuraRedSurtidor[CaraLectura].Activa)//DCF19/01/2018
                    {

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        {

                            if (EstructuraRedSurtidor[CaraTmp].Estado == EstadoCara.Espera)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {


                                CaraEncuestada = CaraTmp;
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                               

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();
                                while (!EncuentaFinalizada)
                                {
                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Esperando fin de encuesta");
                                    //SWRegistro.Flush();
                                    System.Threading.Thread.Sleep(100);

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))   
                                }

                                foreach (Grados Grado in EstructuraRedSurtidor[CaraTmp].ListaGrados) //Recorre las mangueras de la cara encuestada //DCF 06/07/2018
                                {
                                    EstructuraRedSurtidor[CaraTmp].GradoCara = Grado.NoGrado; //DCF para tomar los totalizadores de todas las mangueras 

                                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                                    {
                                        
                                        Lecturas += (Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                                        Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].Lectura) + "|");

                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                            EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                                            EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].Lectura);
                                        SWRegistro.Flush();
                                        //}
                                    }
                                    else
                                    {
                                           Lecturas = "E_ No acepto comando";
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Error| No se tomaron las lecturas");
                                        SWRegistro.Flush();

                                    }
                                }

                              
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Inconsistencia|Cara No esta en Reposo. Estado: " + EstructuraRedSurtidor[CaraTmp].Estado);
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
                    if (EstructuraRedSurtidor[CaraLectura].Activa)//DCF19/01/2018
                    {
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        {
                            if (EstructuraRedSurtidor[CaraTmp].Estado == EstadoCara.Espera)//si esta en reposo envi el proceso de lecturas por surtidor                   
                            {

                                CaraEncuestada = CaraTmp;
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                                
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();

                                while (!EncuentaFinalizada)
                                {
                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Esperando fin de encuesta");
                                    //SWRegistro.Flush();
                                    System.Threading.Thread.Sleep(100);

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                }


                                foreach (Grados Grado in EstructuraRedSurtidor[CaraTmp].ListaGrados) //Recorre las mangueras de la cara encuestada //DCF 06/07/2018
                                {
                                    EstructuraRedSurtidor[CaraTmp].GradoCara = Grado.NoGrado; //DCF para tomar los totalizadores de todas las mangueras 

                                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                                    {

                                        Lecturas += (Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                                        Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].Lectura) + "|");

                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                            EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                                            EstructuraRedSurtidor[CaraTmp].ListaGrados[Grado.NoGrado].Lectura);
                                        SWRegistro.Flush();
                                        //}
                                    }
                                    else
                                    {
                                        Lecturas = "E_ No acepto comando";
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Error| No se tomaron las lecturas");
                                        SWRegistro.Flush();

                                    }
                                }

                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Inconsistencia|Cara No esta en Reposo. Estado: " + EstructuraRedSurtidor[CaraTmp].Estado);
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