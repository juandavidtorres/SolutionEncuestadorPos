
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;
using POSstation.Protocolos;

namespace POSstation.Protocolos
{
    public class Tokheim:iProtocolo
    {
        #region EventoDeProtocolo

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
            ObtenerEstado,
            ObtenerGrado,
            EstablecerPrecio,
            Inicializar,
            AutorizarDespacho,
            ObtenerDespacho,
            ObtenerTotalizador,
            DetenerSurtidor,
            DetenerSurtidores,
            ReanudarVenta
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

        bool Activado_FX_A5_B5 = false;
        bool Activado_CX_DX_A1 = false;
        bool Activado_CX_DX_A3 = false;
        bool Activado_Totaliza_A9 = false;

        string strPrecio;
        string strValorImporte;
        string strValorVolumen;



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
        string Puerto;

        #endregion

        public Tokheim(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
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
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2010.05.13 0250 ");// Modificado 2010.04.12//2010.01.26-1400
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2010.07.06 1737"); //Alias DCF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2010.08.09 1737"); //Alias DCF //Dimensión de Archivos de logueo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.16 1411"); //tiempo de encuesta y log por dias
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.16 1540"); //tiempo de encuesta y log por dias
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.19 1647"); //Precio >9999 y predeterminacion >999.999 - 19/08/2011
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.22 1016"); //Cambio de precio viene dividido por el FactorMultiplicadorPrecioVenta lo debo Multiplicar
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.24 1248"); // DCF precio Terpel 23/08/2011;; MultiplicadorPrecioVenta lo debo Multiplicar
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.09.23 1605"); //Reset del elemento que indica que la Cara debe ser autorizada 23/09/2011
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.01.18 0902"); //para terpel. Precios de Importe superiores a 999999 se envia el importe calculado Imp = Vol * PV
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.03.15 1815"); //  case EstadoCara.TokheimVentaDetenida98: error en el log
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.03.17 1133"); // DCF 16-03-2012 ReanudarVenta
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.03.19 1007"); // NO ReanudarVenta
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.07.06 0835"); //if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)-- 06/07/2012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2013.10.31 1518");//Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2018.03.08 1740");//DCF Archivos .txt 08/03/2018  
                SWRegistro.Flush();





                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
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
                                " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1);
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
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Tokheim";
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }
        public Tokheim(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco )
        {
            try
            {
                ActivaID = true; // DCF
                this.AplicaServicioWindows=true;

                this.Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas =Application.StartupPath  + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2010.05.13 0250 ");// Modificado 2010.04.12//2010.01.26-1400
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2010.07.06 1737"); //Alias DCF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2010.08.09 1737"); //Alias DCF //Dimensión de Archivos de logueo
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.16 1411"); //tiempo de encuesta y log por dias
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.16 1540"); //tiempo de encuesta y log por dias
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.19 1647"); //Precio >9999 y predeterminacion >999.999 - 19/08/2011
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.22 1016"); //Cambio de precio viene dividido por el FactorMultiplicadorPrecioVenta lo debo Multiplicar
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.08.24 1248"); // DCF precio Terpel 23/08/2011;; MultiplicadorPrecioVenta lo debo Multiplicar
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2011.09.23 1605"); //Reset del elemento que indica que la Cara debe ser autorizada 23/09/2011
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.01.18 0902"); //para terpel. Precios de Importe superiores a 999999 se envia el importe calculado Imp = Vol * PV
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.03.15 1815"); //  case EstadoCara.TokheimVentaDetenida98: error en el log
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.03.17 1133"); // DCF 16-03-2012 ReanudarVenta
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.03.19 1007"); // NO ReanudarVenta
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2012.07.06 0835"); //if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)-- 06/07/2012
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2013.10.31 1518");//Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Tokheim_Mixto. Modificado 2018.03.08 1740");//DCF Archivos .txt 08/03/2018 
                SWRegistro.Flush();




               
                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
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
                                " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 );
                    }

                    //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                    //esto funciona siempre y cuando no se hagan mas venta en la cara. 
                    if (Convert.ToInt32(oCara.GradoMangueraVentaParcial) >= 0)
                    {
                        SWRegistro.WriteLine("*********************************** *************************************************** ");
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Recuperar Venta para el Grado: " +  oCara.GradoMangueraVentaParcial + " - Venta Parcial: " + oCara.EsVentaParcial);
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
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Tokheim";
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
                        //Si la cara está activa, realizar proceso de encuesta
                        if (ORedCaras.Activa == true)
                        {
                            CaraEncuestada = ORedCaras.Cara;
                            CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF
                            // ******* ******* ******* ******* DCF ******* ******* ******* ******* ******* *******

                                //if (ActivaID == true) //
                                //{
                                //    GrupoSurtidor();// Se realiza el proceso de ID por el numero de caras 
                                //}

                        //******* ******* ******* ******* DCF ******* ******* ******* ******* ******* *******
                                //else
                                //{
                                //NumMangueraCara = EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count; // Numeros de mangueras

                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerEstado))
                                    TomarAccion();
                            //}
                        }
                        Thread.Sleep(20);
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CicloCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        //#endregion



        //public void VerifySizeFile()
        //{
        //    try
        //    {
        //        FileInfo FileInf = new FileInfo(ArchivoTramas);

        //        if (FileInf.Length > 50000000)
        //        {
        //            SWTramas.Close();
        //            ArchivoTramas =Application.StartupPath  + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Tramas.(" + PuertoCom.PortName + ").txt";
        //            SWTramas = File.AppendText(ArchivoTramas);
        //        }



        //        FileInf = new FileInfo(Archivo);
        //        if (FileInf.Length > 30000000)
        //        {
        //            SWRegistro.Close();
        //            //Crea archivo para almacenar inconsistencias en el proceso logico
        //            Archivo =Application.StartupPath  + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Sucesos(" + PuertoCom.PortName + ").txt";
        //            SWRegistro = File.AppendText(Archivo);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //    }

        //}


        public void VerifySizeFile()
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Tramas.(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Tokheim-Sucesos(" + Puerto + ").txt";
                    SWRegistro = File.AppendText(Archivo);
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|VerifySizeFile: " + Excepcion);
                SWRegistro.Flush();
            }

        }


        // ********************************************DCF  ID y SW *******************************************
        // ***********************************************// *******************************************


        private void GrupoSurtidor()
        {

            CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF
            if (EstructuraRedSurtidor[CaraEncuestada].CaraInicializada = true)

                ContaID = ContaID + 1;

            //for (int i = 0; i <= EstructuraRedSurtidor.Count; i++)
            {

                BytesEsperados = 2;
                TimeOut = 200;

                //Obtener Datos Despacho y Totalizadores GST
                TramaTx = new byte[4];
                TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                TramaTx[2] = 0xB0;

                Redundancia(); // se Completa la trama con los bit de Redundacia.
                EnviarComando();
                RecibirInformacion();


                ID2 = TramaRx[0];

                string hexID2 = ID2.ToString("X");
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| " + "B0 = " + " " + hexID2);
                SWRegistro.Flush();



                BytesEsperados = 2;
                TimeOut = 200;

                //Obtener Datos Despacho y Totalizadores GST
                TramaTx = new byte[4];
                TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                TramaTx[2] = 0xA0;

                Redundancia(); // se Completa la trama con los bit de Redundacia.
                EnviarComando();
                RecibirInformacion();



                ID1 = TramaRx[0];


                string hexID1 = ID1.ToString("X");
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| " + "A0 = " + " " + hexID1);
                SWRegistro.Flush();

                // ****************************************IDENTIFICACION  ID ***********************************************


                #region GrupoI: TCS/MMD, Retron, 162/MMD, 1200, 262 Dom,

                // Grupo I TCS/MD, Retron, 162/MMD, 1200, 262 Dom

                if ((ID1 == 0x98) | (ID1 == 0x9D) | (ID1 == 0x99) | (ID1 == 0x97) | (ID1 == 0x9C) | (ID1 == 0x9B) | (ID1 == 0x2B) | (ID1 == 0x96) | (ID1 == 0x9A) | (ID1 == 0x9D) | (ID1 == 0x29))
                {

                    //TipoSurtidorI_TCS_MMD = CaraEncuestada; // Se Asigna la cara que coresponde al Modelo o Grupo

                    hexID1 = ID1.ToString("X");
                    hexID2 = ID2.ToString("X");
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| Grupo I TCS/MD, Retron, 162/MMD, 1200, 262 Dom |" + " " + "ID =" + hexID1 + "  " + "Software R,N =" + hexID2);
                    SWRegistro.Flush();

                }

                #endregion

                #region GrupoII: Euro 162, 262 int'1,UK Blender, S.A. Bullet, Retron.

                // Grupo I TCS/MD, Retron, 162/MMD, 1200, 262 Dom

                if ((ID1 == 0x98) | (ID1 == 0x9D) | (ID1 == 0x94) | (ID1 == 0x91) | (ID1 == 0x2D))
                {

                    //TipoSurtidorI_TCS_MMD = CaraEncuestada; 

                    hexID1 = ID1.ToString("X");
                    hexID2 = ID2.ToString("X");
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| GrupoII: Euro 162, 262 int'1,UK Blender, S.A. Bullet, Retron. |" + " " + "ID =" + hexID1 + "  " + "Software R,N =" + hexID2);
                    SWRegistro.Flush();

                }

                #endregion

                #region Grupo III: TCS, MMD/TCS y Model 242/244.
                if ((ID1 == 0x45) | (ID1 == 0x55) | (ID1 == 0xA5) | (ID1 == 0xB5) | (ID1 == 0x26) | (ID1 == 0x36) | (ID1 == 0x86) | (ID1 == 0x96) | (ID1 == 0x06))
                {


                    //TipoSurtidorIII_TCS = CaraEncuestada; 
                    hexID1 = ID1.ToString("X");
                    hexID2 = ID2.ToString("X");
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| Grupo III; TCS, MMD/TCS y Model 242/244  |" + " " + "ID =" + hexID1 + "  " + "Software R,N =" + hexID2);
                    SWRegistro.Flush();

                }
                #endregion

                #region Grupo III:Blender y 262A
                if ((ID1 == 0x20) | (ID1 == 0x30) | (ID1 == 0x80) | (ID1 == 0x90) | (ID1 == 0x26) | (ID1 == 0x29) | (ID1 == 0x86) | (ID1 == 0x99))
                {

                    //TipoSurtidorIII_262A_Blender = CaraEncuestada; 

                    hexID1 = ID1.ToString("X");
                    hexID2 = ID2.ToString("X");
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| Grupo III: Blender y 262A  |" + " " + "ID =" + hexID1 + "  " + "Software R,N =" + hexID2);
                    SWRegistro.Flush();

                }

                #endregion

                #region GrupoIII: TCS Taiwan y 262A Taiwan
                if ((ID1 == 0x26) | (ID1 == 0x29) | (ID1 == 0x86) | (ID1 == 0x99) | (ID1 == 0x36) | (ID1 == 0x86) | (ID1 == 0x96))
                {

                    //TipoSurtidorIII_262A_T = CaraEncuestada;

                    hexID1 = ID1.ToString("X");
                    hexID2 = ID2.ToString("X");
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| GrupoIII: TCS Taiwan y 262A Taiwan |" + " " + "ID =" + hexID1 + "  " + "Software R,N =" + hexID2);
                    SWRegistro.Flush();
                    //}  
                }


                #endregion


            }



            if (EstructuraRedSurtidor.Count == ContaID)
            {
                ActivaID = false;
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
                int MaximoReintento = 2;

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

                //Arma la trama de Transmision

                // ******* ******* ******* *******  DCF ******* ******* ******* ******* ******* 
                //if (ActivaID == false)
                //{
                    ArmarTramaTx();
                //}
                // ******* ******* ******* *******  DCF ******* ******* ******* ******* ******* 


                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {
                    EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor
                    //if (BytesEsperados > 0)
                    //{
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
                    case ComandoSurtidor.ObtenerDespacho: //0k
                        //Valores de comunicación propios del comando
                        BytesEsperados = 18;
                        TimeOut = 200;

                        #region Obtener Datos Despacho y Totalizadores GST
                        TramaTx = new byte[4];
                        TramaTx[0] = (byte)(0xF0 | (EstructuraRedSurtidor[CaraEncuestada].Cara - 1));
                        TramaTx[2] = 0xA1;
                        #endregion

                        break;

                    case ComandoSurtidor.ObtenerEstado://ok
                        //Valores de comunicación propios del comando
                        BytesEsperados = 2;
                        TimeOut = 150;

                        #region Obtener Estado GST
                        TramaTx = new byte[4];
                        TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                        TramaTx[2] = 0xA2;
                        #endregion

                        break;

                    case ComandoSurtidor.DetenerSurtidor:
                        //Valores de comunicación propios del comando
                        BytesEsperados = 2;
                        TimeOut = 150;


                        #region Detener Surtidor GST
                        TramaTx = new byte[4];
                        TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                        TramaTx[2] = 0xA3;
                        #endregion

                        break;

                    case ComandoSurtidor.AutorizarDespacho://ok

                        ComandoCaras = ComandoSurtidor.AutorizarDespacho;
                        Activado_FX_A5_B5 = true;
                        //Valores de comunicación propios del comando
                        BytesEsperados = 2;
                        TimeOut = 200;

                        #region Autorizar Despacho GST
                        TramaTx = new byte[22];

                        if (EstructuraRedSurtidor[CaraEncuestada].ComandoFX_A5_B5 == false)
                        {
                            TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                            TramaTx[2] = 0XA5;
                        }

                        else
                        {
                            TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                            TramaTx[2] = 0XB5;
                        }

                        TramaTx[4] = 0x04;

                        //Valor de precio

                        strPrecio = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 *
                                    EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)).PadLeft(4, '0');

                        //Almacena los dos dígitos MENOS SIGNIFICATIVOS de la cifra del PRECIO en la parte alta del byte
                        TramaTx[6] = Convert.ToByte(strPrecio.Substring(2, 2), 16);

                        //Almacena los dos dígitos MAS SIGNIFICATIVOS de la cifra del PRECIO en la parte baja del byte
                        TramaTx[8] = Convert.ToByte(strPrecio.Substring(0, 2), 16);



                        ////Control del importe a programar en mangueras con precio de venta >9999
                        //if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 > 9999)
                        //{
                        //   EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado =
                        //   EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado / EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;
                        //}

                        //Valor de Predeterminacion en $$
                        if(EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                        {
                            if (EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado <= 999999) //DCF 18/08/2011 DCF precio Terpel 23/08/2011;;
                                {
                                    strValorImporte = Convert.ToString(Convert.ToInt32((EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado *
                                    EstructuraRedSurtidor[CaraEncuestada].FactorImporte)/EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta)).PadLeft(6, '0');

                                    strValorVolumen = Convert.ToString(Convert.ToUInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado /
                                    ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1)  
                                    * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta) * EstructuraRedSurtidor[CaraEncuestada].FactorVolumen)).PadLeft(6, '0');
                                }
                                else
                                {
                                    strValorImporte = "000000";

                                   
                                    strValorVolumen = Convert.ToString(Convert.ToUInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado /
                                   ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1)
                                   * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta) * EstructuraRedSurtidor[CaraEncuestada].FactorVolumen)).PadLeft(6, '0');
                                }                            

                        }
                        else if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen) 
                        {

                            strValorImporte = "000000";

                            strValorVolumen = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado *
                            EstructuraRedSurtidor[CaraEncuestada].FactorVolumen)).PadLeft(6, '0');

                        }
                        else
                        {
                            strValorImporte = "000000";
                            strValorVolumen = "999999";
                        }

                        //Almacena los dos dígitos MENOS SIGNIFICATIVOS de la cifra del DINERO en la parte alta del byte
                        TramaTx[10] = Convert.ToByte(strValorImporte.Substring(4, 2), 16);

                        //Almacena los dos dígitos MEDIOS de la cifra del DINERO en la parte media del byte
                        TramaTx[12] = Convert.ToByte(strValorImporte.Substring(2, 2), 16);

                        //Almacena los dos dígitos MAS SIGNIFICATIVOS de la cifra del DINERO en la parte baja del byte
                        TramaTx[14] = Convert.ToByte(strValorImporte.Substring(0, 2), 16);

                        //Almacena los dos dígitos MENOS SIGNIFICATIVOS de la cifra del VOLUMEN en la parte alta del byte
                        TramaTx[16] = Convert.ToByte(strValorVolumen.Substring(4, 2), 16);

                        //Almacena los dos dígitos MEDIOS de la cifra del VOLUMEN en la parte media del byte
                        TramaTx[18] = Convert.ToByte(strValorVolumen.Substring(2, 2), 16);

                        //Almacena los dos dígitos MAS SIGNIFICATIVOS de la cifra del VOLUMEN en la parte baja del byte
                        TramaTx[20] = Convert.ToByte(strValorVolumen.Substring(0, 2), 16);

                        #endregion

                        break;

                    case ComandoSurtidor.Inicializar://ok
                        //Valores de comunicación propios del comando
                        BytesEsperados = 2;
                        TimeOut = 150;

                        #region Inicializar Cara GST
                        TramaTx = new byte[20];
                        TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                        TramaTx[2] = 0xA6;

                        //Valor de precio

                        strPrecio = Convert.ToString(Convert.ToInt16(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 *
                                    EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)).PadLeft(4, '0');

                        //Almacena los dos dígitos MENOS SIGNIFICATIVOS de la cifra del PRECIO en la parte alta del byte
                        TramaTx[4] = Convert.ToByte(strPrecio.Substring(2, 2), 16);

                        //Almacena los dos dígitos MAS SIGNIFICATIVOS de la cifra del PRECIO en la parte baja del byte
                        TramaTx[6] = Convert.ToByte(strPrecio.Substring(0, 2), 16);

                        #endregion

                        break;


                    case ComandoSurtidor.ObtenerTotalizador://ok

                        Activado_Totaliza_A9 = true;
                        ComandoCaras = ComandoSurtidor.ObtenerTotalizador;
                        #region Obtener Totalizador GST


                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 1) //PARA 2 MANGUERAS POR CARA
                        {
                            //if (EstructuraRedSurtidor[CaraEncuestada].Comando262A_A9 == false) // FALSO PARA BE= 84, True = 34
                            //    {
                            //Valores Para Surtidores Tokheim
                            BytesEsperados = 82;
                            TimeOut = 300;
                            TramaTx = new byte[6]; // SURTIDORES DE 2 MANGUERA POR CARA
                            TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                            TramaTx[2] = 0xA9;
                        }

                        else //PARA 1 MANGUERA POR CARA 
                        {
                            //Valores Para Surtidores 262A
                            BytesEsperados = 34;
                            TimeOut = 300;
                            TramaTx = new byte[4]; // SURTIDORES DE 1 MANGUERA POR CARA
                            TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                            TramaTx[2] = 0xA9;
                        }

                        #endregion
                        break;


                    case ComandoSurtidor.ObtenerGrado://0k                        

                        Activado_CX_DX_A1 = true;
                        //Valores de comunicación propios del comando
                        BytesEsperados = 2;
                        TimeOut = 150;
                        #region Obtener Grado GST
                        TramaTx = new byte[4];

                        if (EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A1 == false)
                        {
                            TramaTx[0] = (byte)(0xD0 | (CaraEncuestada - 1));
                            TramaTx[2] = 0XA1;
                        }

                        else
                        {
                            TramaTx[0] = (byte)(0xC0 | (CaraEncuestada - 1));
                            TramaTx[2] = 0XA1;
                        }

                        #endregion

                        break;

                    case ComandoSurtidor.EstablecerPrecio:

                        Activado_CX_DX_A3 = true;
                        //Valores de comunicación propios del comando

                        TimeOut = 300;
                        #region Asignar Precio GST

                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 1) //para Cara con mas de 1 manguera
                        //if (EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A3 == false)
                        {
                            BytesEsperados = 2; // para T.Premier
                            TramaTx = new byte[20];//para 4 productos
                            TramaTx[0] = (byte)(0xD0 | (CaraEncuestada - 1)); //0xC0
                            TramaTx[2] = 0XA3;
                        }

                        else
                        {
                            BytesEsperados = 0; //0; para 262A ó Para surtidores con 1 manguera por Cara 
                            TramaTx = new byte[20]; // para 1 Producto
                            TramaTx[0] = (byte)(0xD0 | (CaraEncuestada - 1));
                            TramaTx[2] = 0XA3;
                        }


                        int IndiceTrama = 4;
                        //Se recorre los grado de la cara para asignar el valor
                        foreach (Grados oGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                        {
                            //strPrecio = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].PrecioNivel1 *
                            //EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)).PadLeft(4, '0'); //DCF ToInt32 por ToInt16 OverflowException:

                             //DCF si el precio es menor a 10.000 no divida el valor enviar el precio normal

                            //EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].PrecioNivel1 =
                            //    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].PrecioNivel1 * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta; //DCF 2011.08.22                       
                       

                            //if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].PrecioNivel1 > 9999)
                            //{
                            //    strPrecio = Convert.ToString((Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].PrecioNivel1 *
                            //    EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)) / EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta).PadLeft(4, '0'); // DCF 5 digitos
                            //}
                            //else
                            //{
                            strPrecio = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].PrecioNivel1 * EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)).PadLeft(4, '0'); 
                            //}

                       
                            //Almacena los dos dígitos MAS SIGNIFICATIVOS de la cifra del PRECIO en la parte alta del byte
                            TramaTx[IndiceTrama] = Convert.ToByte(strPrecio.Substring(0, 2), 16);

                            //Almacena los dos dígitos MENOS SIGNIFICATIVOS de la cifra del PRECIO en la parte baja del byte
                            TramaTx[IndiceTrama + 2] = Convert.ToByte(strPrecio.Substring(2, 2), 16);

                            //Aumenta contador
                            IndiceTrama += 4;
                        }

                        #endregion

                        break;


                    case ComandoSurtidor.ReanudarVenta: // DCF 16-03-2012 ReanudarVenta

                        //Valores de comunicación propios del comando
                        BytesEsperados = 2;
                        TimeOut = 100;


                        #region Detener Surtidor GST
                        TramaTx = new byte[4];
                        TramaTx[0] = (byte)(0xF0 | (CaraEncuestada - 1));
                        TramaTx[2] = 0xA4;
                        #endregion
                        
                        break;




                }

                //Calcula los bytes de redundancia
                for (int i = 1; i < TramaTx.Length; i += 2)
                    TramaTx[i] = Convert.ToByte(0xFF - TramaTx[i - 1]);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de el método ArmarTramaTx";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }



        private void Redundancia() // utilizado para la busqueda de los ID al Iniciar el programa
        {
            //Calcula los bytes de redundancia
            for (int i = 1; i < TramaTx.Length; i += 2)
                TramaTx[i] = Convert.ToByte(0xFF - TramaTx[i - 1]);

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
                //Thread.Sleep(1500); //Solo para Purebas 
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase EnviarComando";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
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

                // ************* DCF **********************
                // Detectar el no recibo de comando y asignación  del  nuevo comando.
                // ************* DCF **********************


                if (BytesEsperados != Bytes) // (Bytes == 0)// Envia un Nuevo Comando en caso que el surtidor no responde al comando enviado.
                {
                    // si no responde al comando, desactiva el comando enviado y activa el sigueinte comando A5 o B5
                    if (Activado_FX_A5_B5 == true) // AUTORIZAR
                    {
                        if (EstructuraRedSurtidor[CaraEncuestada].ComandoFX_A5_B5 == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ComandoFX_A5_B5 = false;
                        }

                        else
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ComandoFX_A5_B5 = true;
                        }

                        Activado_FX_A5_B5 = false;

                        ArmarTramaTx();
                    }

                    // si no responde al comando, desactiva el comando enviado y activa el sigueinte comando CX A1 ó DX A1

                    if (Activado_CX_DX_A1 == true) // OBTENER GRADO 
                    {
                        if (EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A1 == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A1 = false;
                        }

                        else
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A1 = true;
                        }

                        Activado_CX_DX_A1 = false;

                        ArmarTramaTx();
                    }



                    if (Activado_CX_DX_A3 == true) //ESTABLECER PRECIO
                    {
                        if (EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A3 == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A3 = false;
                        }

                        else
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ComandoCX_DX_A3 = true;
                        }

                        Activado_CX_DX_A3 = false;

                        ArmarTramaTx();
                    }



                    if (Activado_Totaliza_A9 == true)// OBTENER TOTALIZADOR
                    {
                        if (EstructuraRedSurtidor[CaraEncuestada].Comando262A_A9 == true)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].Comando262A_A9 = false;
                        }

                        else
                        {
                            EstructuraRedSurtidor[CaraEncuestada].Comando262A_A9 = true;
                        }

                        Activado_Totaliza_A9 = false;

                        ArmarTramaTx();

                    }
                }

            }

            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo RecibirInformacion";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }


            if (Activado_FX_A5_B5 == true)//AUTORIZAR
            {
                Activado_FX_A5_B5 = false;
            }

            if (Activado_CX_DX_A1 == true) //OBTENER GRADO
            {
                Activado_CX_DX_A1 = false;
            }

            if (Activado_CX_DX_A3 == true)//ESTABLECER PRECIO
            {
                Activado_CX_DX_A3 = false;
            }

            if (Activado_Totaliza_A9 == true)//OBTENER TOTALIZADOR
            {
                Activado_Totaliza_A9 = false;
            }

        }

        //REVISA LA INTEGRIDAD DE LA TRAMA
        private bool ComprobarIntegridadTrama()
        {
            try
            {
                //Todos los mensajes que provienen del surtidor vienen en tramas con Bytes pares: Byte Dato y Byte Complemento
                for (int i = 0; i < TramaRx.Length; i += 2)
                {
                    if (TramaRx[i + 1] != 0xFF - TramaRx[i])
                        return false;
                }
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

        #endregion

        #region ANALISIS DE TRAMAS Y RECONSTRUCCIÓN DE DATOS PROVENIENTE DEL SURTIDOR

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void AnalizarTrama()
        {
            try
            {
                switch (ComandoCaras)
                {
                    case ComandoSurtidor.AutorizarDespacho:
                    case ComandoSurtidor.Inicializar:
                    case ComandoSurtidor.ObtenerEstado:
                    case ComandoSurtidor.DetenerSurtidor:
                        RecuperarEstado();
                        break;
                    case ComandoSurtidor.ObtenerDespacho:

                        //DCF 19/08/2011
                        //int GradoMangueraON = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto;

                        //if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 > 9999)
                        //{
                        //DCF precio Terpel 23/08/2011;;
                            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = (ObtenerValor(0, 2) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)
                                                                                * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta; //DCF 5 Digitos

                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta = (ObtenerValor(4, 8) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte)
                                                                                * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta; 

                            EstructuraRedSurtidor[CaraEncuestada].Volumen = ObtenerValor(10, 14) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;

                        //}
                        //else
                        //{
                        //    //Se obtienen los valores obtenidos en la trama

                        //    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = (ObtenerValor(0, 2) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio);
                        //    EstructuraRedSurtidor[CaraEncuestada].TotalVenta = ObtenerValor(4, 8) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;
                        //    EstructuraRedSurtidor[CaraEncuestada].Volumen = ObtenerValor(10, 14) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;
                        //}


                        break;


                    case ComandoSurtidor.ObtenerGrado:
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = TramaRx[0] & 0x0F;
                        break;
                    case ComandoSurtidor.ObtenerTotalizador:
                        int IndiceTramRx = 0;

                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 1) //para Cara con mas de 1 manguera
                        {
                            //Totalizador para la cara con mas una Manguera. IndiceTramRx + 8
                            foreach (Grados oGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].Lectura =
                                    ObtenerValor(IndiceTramRx, IndiceTramRx + 8) / EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador;
                                IndiceTramRx += 20;

                                //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Totalizador para C con 2 manguera" + ObtenerValor);
                                //SWRegistro.Flush();
                            }
                        }

                        else
                        { //Totalizador para la cara con una sola Manguera. IndiceTramRx + 6
                            foreach (Grados oGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].Lectura =
                                    ObtenerValor(IndiceTramRx, IndiceTramRx + 6) / EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador;
                                IndiceTramRx += 20;
                            }

                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Totalizador para C con 1 manguera" + ObtenerValor);
                            //SWRegistro.Flush();
                        }
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
                byte CodigoEstado = TramaRx[0];
                /* Espera               0x20
                 * Transicion           0x24
                 * Teclado              0x2F
                 * Autorizado           0x90
                 * DetenidoTemporizador 0x91
                 * Despacho95           0x95
                 * DetenidoEnergia      0x98
                 * DetenidoMonitoreo    0x99
                 * DetenidoMonitoreo2   0x9D
                 * PorAutorizar         0xA0
                 * DetenidoObturador    0xD0
                 * DespachoD4           0xD4
                 * DespachoF0           0xF0
                 * Indeterminado                 
                 */
                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;

                //Asigna Estado
                switch (CodigoEstado)
                {

                    case (0x2F):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimNoInicializado;
                        break;
                    case (0x20):
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimFinDespacho;
                            //Recuperar venta para el grado especifico fuere de sistema o reinicio de sistema con una Venta en curso DCF 31_10_2013
                            //esto funciona siempre y cuando no se hagan mas venta en la cara. 
                            if (Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].GradoMangueraVentaParcial) >= 0)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].GradoMangueraVentaParcial);
                                EstructuraRedSurtidor[CaraEncuestada].GradoMangueraVentaParcial ="-1";
                            }   
                         

                        }
                        else
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimReposo;
                        break;
                    case (0xA0):
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Fin de despacho forzado en Por Autorizar");
                            SWRegistro.Flush();
                        }
                        else
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimPorAutorizar;
                        break;
                    case (0x90):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimAutorizado;
                        break;
                    case (0xD0):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimDespachoD0;
                        break;
                    case (0xF0):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimDespachoF0;
                        break;
                    case (0x94):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimDespacho94;
                        break;
                    case (0xD4):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimDespachoD4;
                        break;
                    case (0x91):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimFinDespacho91;
                        break;
                    case (0x95):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimFinDespacho95;
                        break;
                    case (0x98):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimVentaDetenida98;
                        break;
                    case (0x9C):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimVentaDetenida9C;
                        break;
                    case (0x99):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimFinDespacho99;
                        break;
                    case (0x9D):
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.TokheimFinDespacho9D;
                        break;
                    default:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Estado Indeterminado: " + CodigoEstado.ToString("X2"));
                        SWRegistro.Flush();
                        break;
                }

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
                    case EstadoCara.TokheimNoInicializado:
                        if (!ProcesoEnvioComando(ComandoSurtidor.Inicializar))
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondio comando de Inicializar Surtidor");
                            SWRegistro.Flush();
                        }
                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                        break;

                    case (EstadoCara.TokheimReposo):
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

                    case (EstadoCara.TokheimPorAutorizar):
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
                            //****** ******* ******* ******* ******* ******* *******
                            //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 1) //PARA MAS DE 1 MANGUERA POR CARA
                            {
                                //Pregunta por el grado
                                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerGrado))
                                {
                                    if (0 <= EstructuraRedSurtidor[CaraEncuestada].GradoCara - 1 &&
                                        EstructuraRedSurtidor[CaraEncuestada].GradoCara <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count)
                                    {

                                        if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                                        {
                                            EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado =
                                                EstructuraRedSurtidor[CaraEncuestada].GradoCara - 1;
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
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Intento de autorizacion en grado Inexistente (Grado " +
                                            EstructuraRedSurtidor[CaraEncuestada].GradoCara + ")");
                                        SWRegistro.Flush();
                                    }
                                }

                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondio comando de obtener Grado");
                                    SWRegistro.Flush();
                                }

                            }

                            //****** ******* ******* ******* ******* ******* *******
                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count <= 1) // PARA 1 MANGUERA POR CARA
                            {
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                                {
                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = 0;
                                    int IdProducto =
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto;
                                    int IdManguera =
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;
                                    string Lectura =
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura.ToString("N3");

                                   
                                    if (AplicaServicioWindows)
                                    {
                                        if (AutorizacionRequerida != null)
                                        {
                                            AutorizacionRequerida(CaraID, IdProducto, IdManguera, Lectura,"");
                                        }
                                    }
                                   
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa requerimiento de autorizacion. Grado: "
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

                                if (!ProcesoEnvioComando(ComandoSurtidor.AutorizarDespacho))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondió comando de Autorizar Despacho");
                                    SWRegistro.Flush();
                                }
                                Reintenos++;
                            } while ((EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimPorAutorizar ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimReposo) &&
                                Reintenos <= 2);

                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimAutorizado ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimDespacho94 ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimDespachoD0 ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimDespachoD4 ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimDespachoF0)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                            }
                        }

                        break;

                    case EstadoCara.TokheimAutorizado:
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

                    case EstadoCara.TokheimDespacho94:
                    case EstadoCara.TokheimDespachoD0:
                    case EstadoCara.TokheimDespachoD4:
                    case EstadoCara.TokheimDespachoF0:
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

                    case EstadoCara.TokheimFinDespacho91:
                    case EstadoCara.TokheimFinDespacho95:
                    case EstadoCara.TokheimFinDespacho99:
                    case EstadoCara.TokheimFinDespacho9D:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho

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
                        break;

                    case EstadoCara.TokheimFinDespacho:
                    case EstadoCara.FinDespachoForzado:
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                            ProcesoFindeVenta();
                        break;



                    // si se detiene la venta en curso se envia la autorizacion.. no poruq een la AUt se debe enviar el valor - importe y se presentaria problema en la ventas predeterminadas ????
                   //DCF 16-03-2012 ReanudarVenta
                    case EstadoCara.TokheimVentaDetenida98:
                    case EstadoCara.TokheimVentaDetenida9C:



                        //int Reintenos1 = 1;
                        //    do
                        //    {
                                //CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //DCF

                                ////Thread.Sleep(100);// tiempo para que el surtidor salga del erro venta detenida
                                //if (!ProcesoEnvioComando(ComandoSurtidor.ReanudarVenta))
                                //{
                                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondió comando de ReanudarVenta");
                                //    SWRegistro.Flush();
                                //}
                                //Reintenos1++;       

                                //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso| ReanudarVenta en venta detenida - Estado 98/9C");
                                //SWRegistro.Flush();

                            //}   while ((EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimVentaDetenida98 ||
                            //        EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimVentaDetenida9C) &&
                            //        Reintenos1 <= 2) ;


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

                    // para importes superiores a 999,999
                    decimal ImporteCalculado = EstructuraRedSurtidor[CaraEncuestada].Volumen * EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;
                    string strTotalVenta = "0";
                   
                    //Evalúa si la venta viene en 0

                    if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0 || EstructuraRedSurtidor[CaraEncuestada].TotalVenta != 0)
                    {
                        //para terpel. Precios de Importe superiores a 999999 se envia el importe calculado Imp = Vol * PV
                            if (ImporteCalculado > 999999)
                            {
                                strTotalVenta = ImporteCalculado.ToString("N3");

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|ImporteCalculado Venta Superior a 999.999");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                            }


                        //Almacena los valores en las variables requerida por el Evento
                        //strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
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

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|MultiplicadorPrecioVenta = |" + EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta);
                        SWRegistro.Flush();//Borrar DCF 

                        String PresionLLenado = "0";
                        if (AplicaServicioWindows)
                        {
                            if (VentaFinalizada != null)
                            {
                                VentaFinalizada(CaraID, strTotalVenta, strPrecio, strLecturaFinalVenta, strVolumen, bytProducto, IdManguera, PresionLLenado, strLecturaInicialVenta);
                            }
                        }
                        
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
                if (ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio))
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 1) //PARA 2 MANGUERAS POR CARA
                    {
                        if (TramaRx[0] == 0xB0)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precios aceptados por Surtidor");
                            SWRegistro.Flush();
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Cambio de precio rechazado por Surtidor");
                            SWRegistro.Flush();
                        }
                    }

                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No respondio comando Establecer Precio");
                    SWRegistro.Flush();
                }

                foreach (Grados Grado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Grado: " + Grado.NoGrado + " - Precio: " +
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioNivel1);
                    SWRegistro.Flush();
                }
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




        #region Evento DE LA CLASE

        //private void Evento_VentaAutorizada(ref byte Cara, ref string Precio, ref string ValorProgramado, ref byte TipoProgramacion, ref string Placa, ref int MangueraProgramada, ref bool EsVentaGerenciada)
      
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
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))

                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1) /
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 23/08/2011;


                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2)/
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 23/08/2011;;
                            }
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1) /
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 23/08/2011;

                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2) /
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 23/08/2011;
                            }
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Evento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_TurnoCerrado( string Surtidores,  string PuertoTerminal)
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
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Fuera de red de surtidores. Evento: Evento_TurnoCerrado");
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
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Fuera de red de surtidores. Evento: Evento_TurnoCerrado");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Evento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)   
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
                string MensajeExcepcion = "Excepción en el Constructor del método Evento_VentaAutorizada";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }
     


       
        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)
        {
            try
            {

                //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
                //foreach (Gasolutions.FabricaProtocoloServicio.Manguera OManguera in mangueras)
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
                string MensajeExcepcion = "Excepcion en el Evento Evento_ProgramarCambioPrecioKardex:" + Excepcion;
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
                string MensajeExcepcion = "Evento_CerrarProtocolo " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                throw Excepcion; //throw new Exception ("Comunicacion con surtidor no disponible");
            }



        }

      
        public void Evento_FinalizarCambioTarjeta(byte Cara) { }
        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto) { }


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
                string MensajeExcepcion = "Excepción en el Evento Evento_FinalizarVentaPorMonitoreoCHIP: " + Excepcion;
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

        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera
        {
        }

        #endregion
    }
}