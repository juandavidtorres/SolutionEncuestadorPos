using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;


//using gasolutions.Protocolos;
using POSstation.Protocolos;
//using POSstation.Protocolos.FabricaProtocolosC.Protocolos.Wayne;
//using POSstation.Protocolos.FabricaProtocolosC.Protocolos.EMR3;
namespace VentanaEncuestadorFullStation
{


    public partial class frmEncuestador : Form
    {

        #region CONSTRUCTOR
        public frmEncuestador()
        {
            InitializeComponent();
            InicializarControles();
        }
        #endregion
        int Protocolo = 0;///1 Tokhein 2 Wayne //3 Gilbarco
        #region DEFINICIONES LOCALES

      
      
        int IdManguera;
     // POSstation.Protocolos.Tokheim_Loop oProtocoloTokhe_Loop;
      POSstation.Protocolos.Tokheim oProtocoloTokheim;
      POSstation.Protocolos.Wayne oProtocoloWayne;
      //POSstation.Protocolos.FabricaProtocolosC.Protocolos.Wayne.Wayne oProtocoloWayne;        
      Gilbarco_Extended oProtocoloGilbarco_Extended;
        //EMR3 oprotocolo_EMR3;
      POSstation.Protocolos.PumpControl oPrptocoloPumpControl;
      POSstation.Protocolos.Develco oProtocoloDevelco;
      //POSstation.Protocolos.FillPos_IMW oProtocoloFillPos;
      POSstation.Protocolos.HongYang oProtocoloHongYang;
      //POSstation.Protocolos.PumpControl oProtocoloH2P_Safe;
      //POSstation.Protocolos.H2P_Safe oProtocoloH2P_Safe; 
      //POSstation.Protocolos.FAFNIR oProtocoloFAFNIR;
      //POSstation.Protocolos.Bennett oProtocoloBennett;
      //POSstation.Protocolos.VeederRoot.VeederRoot oProtocoloveeder;
      POSstation.Protocolos.Chile_2A oprotocolo2A;
      POSstation.Protocolos.GilbarcoKraus oprotocoloGilbarcoKraus; 

      SharedEventsFuelStation.CMensaje oEventos; //Controla la comunicacion entre las aplicaciones

        //Dictionary<int,Tanque> lista=new Dictionary<int,Tanque>();
                        //for (int i = 0; i < 1; i++)
                        //{
                        //    Tanque obj = new Tanque();
                        //    obj.TankNumber = i;
                        //    lista.Add(i,obj);
			 
                        //}
                        //oveeder = new VeederRoot(false, "192.168.168.168", cmbNombrePuerto.Text, lista);

        System.Globalization.CultureInfo miCultura;    

           

       delegate void Del();
       string Mensaje ;
       string Volumen1 = "000000";
       string Importe1 = "000000";
       string Pventa1 = "0000";
       string TotalVolumen1 = "000000";
       string Cara1= "0";

       string precio = "0";
       string precio2 = "0";


       int Caras;

       
        int Grado_M = 0;
        int caraR = 0;

        bool ECO_Loop= true;
        Dictionary<byte, RedSurtidor> EstructuraRedSurtidor;
        List<byte> ListaCara = new List<byte>();

        bool Autorizar_auto = false;

        byte Cara;
        byte TipoProgramacion;
        string ValorProgramado;

        bool extd; 
        #endregion

        #region METODOS
        private void InicializarControles()
        { 
            //Encuentra los puertos disponibles en el Computador
            cmbNombrePuerto.Items.Clear();
            foreach (string s in SerialPort.GetPortNames())
                cmbNombrePuerto.Items.Add(s);

            //if (cmbNombrePuerto.Items.Contains(Settings.Default. )) cmbNombrePuerto.Text = Settings.Default.PortName;
            if (cmbNombrePuerto.Items.Count > 0) 
            {
                //cmbNombrePuerto.SelectedIndex = 1;
                btnIniciar.Enabled = true;
                
                cmbNombrePuerto.SelectedIndex = 0;
                cmbSurtidor.SelectedIndex = 0;
                cmbCara.SelectedIndex = 0;
                cmbProtocolo.SelectedIndex = 0;

               //void InformarFinalizacionVenta( byte Cara,  string Valor,  string Precio,  string LecturaFinal,  string Cantidad,  byte Producto,  int Manguera,  string presionLLenado,  string lecturaInicial);
        
            }
            else
            {
              //  MessageBox.Show(this, "No se ha detectado ningún puerto COM en este computador.\nPor favor instale un puerto COM y reinicie esta aplicación.", "No hay puerto COM instalado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //this.Close();
                btnIniciar.Enabled = true;
            }
        }
        #endregion


        #region EVENTOS DE LA CARA

        //EVENTOS DE LA CARA

        //private void oEvento_RequerirAutorizacion( Cara,  IdProducto,  IdManguera,  Lectura);
        //{


        // }

        private void oEvento_CaraEnReposo( byte Cara,  long IdManguera)
        {
            //SW.WriteLine(Cara + ".Reposo.");
            //SW.Flush();
          // delegate void DelActualizarTexto<string>();
            //ActualizarTexto(( "Manguera " + IdManguera + " en Reposo"))
                //( "Manguera " + IdManguera + " en Reposo")
            Mensaje ="Manguera " + IdManguera + " en Reposo";
            //Application.DoEvents();
            Estadocara.BeginInvoke(new Del(ActualizarTexto));
           
        }

        private void ActualizarTexto()
        {

            try
            {

                //decimal Pventa1_1 = Convert.ToDecimal(Convert.ToInt32(Pventa1.ToString(),16));

                Estadocara.Text = Mensaje;
                Volumen_Despacho.Text = Volumen1;
                Importe_Despacho.Text = Importe1;
                CaraEncuestada.Text = Cara1;

                if (Pventa1 != "0")
                Precio_Venta.Text = Pventa1;

                Total_Vol.Text = TotalVolumen1;


                groupBox4.Text = "Cara En Venta : " + Cara1;
                Application.DoEvents();


           }

            catch (InvalidCastException e) 
            {
            }
      

        }

        //void InformarFinalizacionVenta( byte Cara,  string Valor,  string Precio,  string LecturaFinal,  string Cantidad,  byte Producto,  int Manguera,  string presionLLenado,  string lecturaInicial);
        //private void oEventos_VentaFinalizada( byte Cara,  string strTotal,  string strPrecio,  string LecturaFinalVenta,  string strVolumen,  byte Producto,  int Manguera,  string PresionLlenado,  string LecturaInicialVenta)
        //{

        //    Volumen1 = strVolumen;
        //    Importe1 = strTotal;
        //    Cara1 = Convert.ToString(Cara);
        //    Pventa1 = strPrecio;
        //    TotalVolumen1 = LecturaFinalVenta;


        //    Mensaje = "Venta Finalizada";

        //    Estadocara.BeginInvoke(new Del(ActualizarTexto));

        //    //SW.WriteLine(Cara + ".Fin Venta.");
        //    //SW.Flush();
        //}
       

        private void oEventos_VentaFinalizada( byte Cara,  string Valor,  string Precio,  string LecturaFinal,  string Cantidad,  string Producto,  long Manguera,  string presionLLenado,  string lecturaInicial)
        {

            Volumen1 = Cantidad;
            Importe1 = Valor;
            Cara1 = Convert.ToString(Cara);
            Pventa1 = Precio;
            TotalVolumen1 = LecturaFinal;

            Mensaje = "Venta Finalizada";

            Estadocara.BeginInvoke(new Del(ActualizarTexto));

        }


        private void oEvento_VentaParcial( byte Cara,  string strTotalVenta,  string strVolumen)
        {
            Volumen1 = strVolumen;
            Importe1 = strTotalVenta;
            Cara1 = Convert.ToString(Cara);


            Mensaje = "En Despacho";

           
            Estadocara.BeginInvoke(new Del(ActualizarTexto));


            
           
            //SW.WriteLine(Cara + ".Despachando.");
            //SW.Flush();
        }

        // string[] DatosAutorizacion = { CaraID.ToString(), IdProducto.ToString(), IdManguera.ToString(), Lectura, guidautorizacion };

        private void oEvento_RequerirAutorizacion(  byte Cara_A,  long Producto,  long Manguera,  string Lectura, string guidautorizacion )
        {
            Cara = Cara_A;

            IdManguera = Convert.ToInt32(Manguera);
            Mensaje = "Manguera levantada";
            TotalVolumen1 = Lectura;
            Estadocara.BeginInvoke(new Del(ActualizarTexto));


            if (Autorizar_auto)
            {
                Autorizacio_Automatica();
            }
                
        //{//Autorizacion Automatica 14/04/2012
            

        //    byte TipoProgramacion;
        //    string ValorProgramado;
        //    if (txtPredeterminar.Text == "")
        //        ValorProgramado = "0";
        //    else
        //        ValorProgramado = txtPredeterminar.Text;






            
        //    if (Cara % 2 != 0)
        //    {
        //        precio = PV_MangueraG0.Text;
        //    }
        //    else
        //    {
        //        precio = PV_MangueraG1.Text;
        //    }


        //    string Precio = precio;



        //    if (rdbImporte.Checked)
        //        TipoProgramacion = 0; //1 predetermina Volumen, 0 predetermina Dinero
        //    else
        //        TipoProgramacion = 1;
        //    string Placa = "";
        //    bool EsVentaGerenciada = true;
        //    int IdMangueraProgramada = -1;
        //    oEventos.AutorizarVenta( Cara,  Precio,  ValorProgramado,  TipoProgramacion,  Placa,  IdMangueraProgramada,  EsVentaGerenciada);




          
          
        
        //    CaraEncuestada.Text = Convert.ToString(Cara);

        //    //if (Pventa1 != "0")
        //        Precio_Venta.Text = Precio;

          

        //    Application.DoEvents();


        //}

            //SW.WriteLine(Cara + ".Por Autorizar.");
            //SW.Flush();
        }
        private void oEventos_ErrorComunicacion( byte Cara)
        {
            //SW.WriteLine(Cara + ".Error Comunicación.");
            //SW.Flush();
        }
        private void oEventos_LecturaInicialVenta( byte Cara,  string LecturaInicialVenta)
        {
            TotalVolumen1 = LecturaInicialVenta;

            Volumen1 = "0";
            Importe1 = "0";
            Cara1 = Convert.ToString(Cara);
            Pventa1 = "0";
          


            //SW.WriteLine(Cara + ".Iniciales Venta.");
            //SW.Flush();
        }
    
        
        private void oEventos_LecturaTurnoAbierto( System.Array Lectura)
        {

            T_ON.Visible = true;
            T_off.Visible = false;

          
            //SW.WriteLine(".Iniciales Turno.");
            //SW.Flush();
        }
        private void oEventos_LecturaTurnoCerrado( System.Array Lectura)
        {
            T_off.Visible = true;
            T_ON.Visible = false;

            //SW.WriteLine(".Finales Turno.");
            //SW.Flush();
        }

        private void oEventos_CerrarProtocolo( System.Array Lectura)
        {

        }

        private void oEventos_CambiarDensidad( string desnsidad)
        {

        }


        #endregion

        private void btnIniciar_Click(object sender, EventArgs e)
        {
            try
            {
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
            

                int Manguera = 1;
                Grado_M = Convert.ToInt16(Manguera_cara.Text);
                Caras = Convert.ToInt16(NumCaras.Text);


               // if (radioButton1.Checked)
                {

                    //Estadocara.Text = "Manguera " + IdManguera + " en Reposo";


                    for (byte i = 1; i <= Caras; i++) //6
                    {
                        RedSurtidor Red = new RedSurtidor();
                        Red.Cara = Convert.ToByte(i);
                        Red.CaraBD = Red.Cara;
                        //Red.CaraBD = Convert.ToByte(Red.Cara + 14); //sopa ra utilizar los alias para mas de 16 mangueras 
                        if (i % 2 == 0)
                        {
                            Red.IdSurtidor = i / 2;
                            Red.Activa = true;
                        }
                        else
                        {
                            Red.IdSurtidor = (i + 1) / 2;
                            Red.Activa = true;
                        }

                        double FactorPV = Convert.ToDouble(FactorPrecioFRM.Text);
                        FactorPV = Math.Pow(10, FactorPV);// factor Precio Venta

                        double FactorVolumen = Convert.ToDouble(FactorVolumenFRM.Text);
                        FactorVolumen = Math.Pow(10, FactorVolumen);// Factor Volumen 

                        double FactorImporte = Convert.ToDouble(FactorImporteFRM.Text);
                        FactorImporte = Math.Pow(10, FactorImporte);// Factor Importe 

                        double FactorTotalizador = Convert.ToDouble(FactorTotalizadorRM.Text);
                        FactorTotalizador = Math.Pow(10, FactorTotalizador);// Factor Totalizador 

                        double FactorPredeterminacionVolumen = Convert.ToDouble(FactorPredeterminacioVol.Text);
                        FactorPredeterminacionVolumen = Math.Pow(10, FactorPredeterminacionVolumen);

                        double FactorPredeterminacionImporte = Convert.ToDouble(FactorPredetImporte.Text);
                        FactorPredeterminacionImporte = Math.Pow(10, FactorPredeterminacionImporte);
                        

                        Red.FactorPrecio = Convert.ToInt16(FactorPV); // 100;//1000; //1 //Develco para peru =1000
                        Red.FactorImporte = Convert.ToInt16(FactorImporte); //1
                        Red.FactorVolumen = Convert.ToInt16(FactorVolumen); // 1000 
                        Red.FactorTotalizador = Convert.ToInt16(FactorTotalizador);// 1000
                        Red.FactorPredeterminacionVolumen = Convert.ToInt16(FactorPredeterminacionVolumen);
                        Red.FactorPredeterminacionImporte = Convert.ToInt16(FactorPredeterminacionImporte);
                        
                        if(rad_Extended.Checked == true)
                        {
                            Red.Gilbarco_Extended = true;
                        }

                        

                        for (int j = 0; j < Grado_M; j++) //// 2 NTERIOR 2 PRODUCTO POR CARA 
                        {
                            Grados GradosCara = new Grados();
                            GradosCara.IdProducto = j;
                            GradosCara.MangueraBD = Manguera;
                            Manguera++;
                            GradosCara.NoGrado = Convert.ToByte(j);
                            GradosCara.Autorizar = true;


                            if (j % 2 != 0)
                            {
                                GradosCara.PrecioNivel1 = Convert.ToDecimal(PV_MangueraG0.Text);//Convert.ToDecimal(1.480);
                                GradosCara.PrecioNivel2 = Convert.ToDecimal(PV_MangueraG0.Text); //Convert.ToDecimal(2.189);
                            }
                            else
                            {
                                GradosCara.PrecioNivel1 = Convert.ToDecimal(PV_MangueraG1.Text); //Convert.ToDecimal(8744);
                                GradosCara.PrecioNivel2 = Convert.ToDecimal(PV_MangueraG1.Text); // Convert.ToDecimal(1.480);
                            }

                            Red.ListaGrados.Add(GradosCara);
                        }
                        EstructuraRedSurtidor.Add(Red.Cara, Red);

                    }

                }





                Protocolo = cmbProtocolo.SelectedIndex;

                bool EsTCPIP = true;
                string DireccionIP = IP.Text;
                string Puerto = Perto_IP.Text;

                switch (cmbProtocolo.SelectedIndex)
                {
                      
                    case 1:

                           EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                        //oProtocoloWayne = new Wayne(EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                        oProtocoloWayne = new Wayne(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                        oProtocoloWayne.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloWayne.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                        oProtocoloWayne.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                        oProtocoloWayne.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloWayne.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                        oProtocoloWayne.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                        oProtocoloWayne.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                        oProtocoloWayne.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);
                        
                        break;
                    case 0:

                          EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                         
                      // oProtocoloTokheim = new Tokheim(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                      oProtocoloTokheim = new Tokheim(EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                        oProtocoloTokheim.AutorizacionRequerida+=new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloTokheim.CaraEnReposo +=new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                        oProtocoloTokheim.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                        oProtocoloTokheim.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloTokheim.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                        oProtocoloTokheim.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                        oProtocoloTokheim.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                        oProtocoloTokheim.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);
                 
                        break;                  


                    case 2:
                        oProtocoloGilbarco_Extended = new Gilbarco_Extended(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                        oProtocoloGilbarco_Extended.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloGilbarco_Extended.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                        oProtocoloGilbarco_Extended.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                        oProtocoloGilbarco_Extended.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloGilbarco_Extended.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                        oProtocoloGilbarco_Extended.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                        oProtocoloGilbarco_Extended.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                        oProtocoloGilbarco_Extended.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);
                 
                         break;


                    case 3:
                         //oprotocolo_EMR3 = new EMR3(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                         //oprotocolo_EMR3.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oprotocolo_EMR3.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         //oprotocolo_EMR3.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         //oprotocolo_EMR3.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oprotocolo_EMR3.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         //oprotocolo_EMR3.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         //oprotocolo_EMR3.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         //oprotocolo_EMR3.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);

                         break;

                    case 4:
                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                         //oPrptocoloPumpControl = new PumpControl(EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                         oPrptocoloPumpControl = new PumpControl(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);

                         oPrptocoloPumpControl.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oPrptocoloPumpControl.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         oPrptocoloPumpControl.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         oPrptocoloPumpControl.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oPrptocoloPumpControl.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         oPrptocoloPumpControl.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         oPrptocoloPumpControl.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         oPrptocoloPumpControl.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);
                       

                         break;


                    case 5:
                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                        oProtocoloGilbarco_Extended = new Gilbarco_Extended(EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                        oProtocoloGilbarco_Extended.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloGilbarco_Extended.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                        oProtocoloGilbarco_Extended.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                        oProtocoloGilbarco_Extended.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                        oProtocoloGilbarco_Extended.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                        oProtocoloGilbarco_Extended.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                        oProtocoloGilbarco_Extended.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                        oProtocoloGilbarco_Extended.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);
                 

                         break;



                    case 6:
                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                         oProtocoloDevelco = new Develco(EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                         oProtocoloDevelco.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oProtocoloDevelco.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         oProtocoloDevelco.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         oProtocoloDevelco.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oProtocoloDevelco.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         oProtocoloDevelco.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         oProtocoloDevelco.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         oProtocoloDevelco.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);


                         break;



                    case 7:
                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                         //oProtocoloFillPos = new FillPos_IMW (EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                         //oProtocoloFillPos.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloFillPos.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         //oProtocoloFillPos.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         //oProtocoloFillPos.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloFillPos.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         //oProtocoloFillPos.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         //oProtocoloFillPos.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         //oProtocoloFillPos.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);


                         break;




                    case 8:
                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                         oProtocoloHongYang = new HongYang(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                         oProtocoloHongYang.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oProtocoloHongYang.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         oProtocoloHongYang.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         oProtocoloHongYang.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oProtocoloHongYang.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         oProtocoloHongYang.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         oProtocoloHongYang.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         oProtocoloHongYang.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);


                         break;

                    case 9:
                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                         //oProtocoloH2P_Safe = new H2P_Safe(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                         //oProtocoloH2P_Safe = new PumpControl(EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                         //oProtocoloH2P_Safe.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloH2P_Safe.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         //oProtocoloH2P_Safe.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         //oProtocoloH2P_Safe.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloH2P_Safe.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         //oProtocoloH2P_Safe.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         //oProtocoloH2P_Safe.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         //oProtocoloH2P_Safe.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);


                         break;

                    case 10:
                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;

                        // oProtocoloFAFNIR = new FAFNIR(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                        Dictionary<int,Tanque> tanque=new Dictionary<int,Tanque>();
                        //oProtocoloFAFNIR = new FAFNIR(EsTCPIP, DireccionIP, Puerto, tanque);


                         break;



                    case 11:
                         //EsTCPIP = true;
                         //DireccionIP = IP.Text;
                         //Puerto = Perto_IP.Text;

                         //oProtocoloBennett = new Bennett(EsTCPIP, DireccionIP, Puerto, EstructuraRedSurtidor, true);
                         //oProtocoloBennett.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloBennett.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         //oProtocoloBennett.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         //oProtocoloBennett.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloBennett.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         //oProtocoloBennett.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         //oProtocoloBennett.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         //oProtocoloBennett.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);

                         break;

                    case 12:

                         EsTCPIP = true;
                         DireccionIP = IP.Text;
                         Puerto = Perto_IP.Text;


                                Dictionary<int,Tanque> lista=new Dictionary<int,Tanque>();
                                for (int i = 0; i < 1; i++)
                                {
                                    Tanque obj = new Tanque();
                                    obj.TankNumber = i;
                                    lista.Add(i,obj);

                                }
                         Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                //oEventos = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                //                oProtocoloveeder = new POSstation.Protocolos.VeederRoot.VeederRoot(EsTCPIP,DireccionIP,Puerto, lista);

                         break;



                    case 15:

                         //oProtocoloTokhe_Loop = new Tokheim_Loop(cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                         //oProtocoloTokhe_Loop.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloTokhe_Loop.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         //oProtocoloTokhe_Loop.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         //oProtocoloTokhe_Loop.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         //oProtocoloTokhe_Loop.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         //oProtocoloTokhe_Loop.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         //oProtocoloTokhe_Loop.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         //oProtocoloTokhe_Loop.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);
                         break;





                    case 14:

                         oprotocolo2A = new Chile_2A (cmbNombrePuerto.Text, EstructuraRedSurtidor, true);
                         oprotocolo2A.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oprotocolo2A.CaraEnReposo += new iProtocolo.CaraEnReposoEventHandler(oEvento_CaraEnReposo);
                         oprotocolo2A.VentaParcial += new iProtocolo.VentaParcialEventHandler(oEvento_VentaParcial);
                         oprotocolo2A.AutorizacionRequerida += new iProtocolo.AutorizacionRequeridaEventHandler(oEvento_RequerirAutorizacion);
                         oprotocolo2A.LecturaInicialVenta += new iProtocolo.LecturaInicialVentaEventHandler(oEventos_LecturaInicialVenta);
                         oprotocolo2A.LecturaTurnoAbierto += new iProtocolo.LecturaTurnoAbiertoEventHandler(oEventos_LecturaTurnoAbierto);
                         oprotocolo2A.LecturaTurnoCerrado += new iProtocolo.LecturaTurnoCerradoEventHandler(oEventos_LecturaTurnoCerrado);
                         oprotocolo2A.VentaFinalizada += new iProtocolo.VentaFinalizadaEventHandler(oEventos_VentaFinalizada);
                         break; 


                }

                grpComandos.Enabled = true;
                btnIniciar.Enabled = false;
            }
            catch (Exception ex)
            {
                string MensajeExcepcion = "Excepción" + ex;
            }
        }



      private void btnAutorizar_Click(object sender, EventArgs e)
        {
           btnAutorizar.BackColor = Color.FromArgb(123, 175, 222);

            Cara = Convert.ToByte(cmbCara.Text);

            Autorizacio_Automatica();
            
        }

        private void Autorizacio_Automatica()
         {

             //byte Cara = Convert.ToByte(cmbCara.Text);

            if (txtPredeterminar.Text == "")
                ValorProgramado = "0";
            else
                ValorProgramado = txtPredeterminar.Text;


            if (Cara % 2 != 0)
            {
                precio = PV_MangueraG0.Text;
            }
            else
            {
                precio = PV_MangueraG1.Text;
            }


            string Precio = precio;



            if (rdbImporte.Checked)
                TipoProgramacion = 0; //1 predetermina Volumen, 0 predetermina Dinero
            else
                TipoProgramacion = 1;
            string Placa = "";
            bool EsVentaGerenciada = true;
            int IdMangueraProgramada = -1;

            decimal pre = 0; 

            string guid = " "; 
            // public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada,string guid)
       
            if (Protocolo==0)
            {
                oProtocoloTokheim.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
            }
            if (Protocolo ==1)
            {
                oProtocoloWayne.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
            }
            if (Protocolo == 2 || Protocolo == 5)
            {
              oProtocoloGilbarco_Extended.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
            }

            if (Protocolo == 4)
            {
                oPrptocoloPumpControl.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
                //oprotocolo_EMR3.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada);
            }



            if (Protocolo == 6)
            {
                oProtocoloDevelco.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
            }

            if (Protocolo == 8)
            {
                oProtocoloHongYang.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
            }


            if (Protocolo == 9)
            {

                // oProtocoloH2P_Safe.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid);
            }



            if (Protocolo == 11)
            {
                //oProtocoloBennett.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
            }


            if (Protocolo == 14)
            {
                
                //oProtocoloTokhe_Loop.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada, guid, pre);
            }

            CaraEncuestada.Text = Convert.ToString(Cara);

            //if (Pventa1 != "0")
            Precio_Venta.Text = Precio;

            Application.DoEvents();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Protocolo == 3)
            {

               //oprotocolo_EMR3.Evento_Predeterminar(0x01,  txtPredeterminar.Text,  0x01);
               // oprotocolo_EMR3.Evento_VentaAutorizada(Cara, Precio, ValorProgramado, TipoProgramacion, Placa, IdMangueraProgramada, EsVentaGerenciada);
            }
        }


        private void Venta_Auto_Click_1(object sender, EventArgs e)
        {
            if (!Autorizar_auto)
            {
                Autorizar_auto = true;
                Venta_Auto.BackColor = Color.FromArgb(123, 175, 222);
                btnAutorizar.BackColor = Color.FromArgb(224, 224, 224);
            }
            else
            {
                Autorizar_auto = false;
                Venta_Auto.BackColor = Color.FromArgb(224, 224, 224);
                btnAutorizar.BackColor = Color.FromArgb(123, 175, 222);

            }


        }

     

        private void btnAbrirTurno_Click(object sender, EventArgs e)
        {
            string strSurtidor = cmbSurtidor.Text;
            string strPuerto = "COM1";
            System.Collections.ArrayList ArrayPrecios = new System.Collections.ArrayList();


            int intSurtidor = Convert.ToInt16(strSurtidor);

            if (Grado_M == 1)
            {
                for (int i = 1; i <= Caras; i++)
                {
                    //int intSurtidor = Convert.ToInt16(strSurtidor);
                    //ArrayPrecios.Add("1|10310|10000|" + (intSurtidor * 6 - 5));
                    //ArrayPrecios.Add("2|12000|8600|" + (intSurtidor * 6 - 4));
                    //ArrayPrecios.Add("3|7100|7400|" + (intSurtidor * 6 - 3));
                    //ArrayPrecios.Add("1|12000|10000|" + (intSurtidor * 6 - 2));
                    //ArrayPrecios.Add("2|8650|8600|" + (intSurtidor * 6 - 1));
                    //ArrayPrecios.Add("3|7100|7400|" + (intSurtidor * 6));



                    /*
                    int intSurtidor = Convert.ToInt16(strSurtidor);

                    // para obtener los precio desde el formulario inicial o de pantalla
                    if (i % 2 != 0)
                    {
                        precio = PV_MangueraG0.Text;
                    }
                    else
                    {
                        precio = PV_MangueraG1.Text;
                    }

                    ArrayPrecios.Add(i + "|" + precio + "|" + precio + "|" + (i+1));
                    */

                   
                    ArrayPrecios.Add(0 + "|" + PV_MangueraG0.Text + "|" + PV_MangueraG0.Text + "|" + (intSurtidor * 1));
                  



                }

            }



            if (Grado_M == 2)
            {
                for (int i = 1; i <= Caras; i++)
                {
                     //               (Producto|P1|P2|Manguera)
                    //int intSurtidor = Convert.ToInt16(strSurtidor);
                    //ArrayPrecios.Add("1|10270|10270|" + (intSurtidor * 1));
                    //ArrayPrecios.Add("2|8490|8490|" + (intSurtidor * 2));

                    //ArrayPrecios.Add("1|10270|10270|" + (intSurtidor * 3));
                    //ArrayPrecios.Add("2|8490|8490|" + (intSurtidor * 4));
                    //}

                    ArrayPrecios.Add(1 + "|" + PV_MangueraG0.Text + "|" + PV_MangueraG0.Text + "|" + (ArrayPrecios.Count +1));
                    ArrayPrecios.Add(2 + "|" + PV_MangueraG1.Text + "|" + PV_MangueraG1.Text + "|" + (ArrayPrecios.Count +1));

                    //  ArrayPrecios.Add(i + "|" + PV_MangueraG0.Text + "|" + PV_MangueraG0.Text + "|" + (intSurtidor * 1));
                    //ArrayPrecios.Add(i + "|" + PV_MangueraG1.Text + "|" + PV_MangueraG1.Text + "|" + (intSurtidor * 2));

                }
               
            }


          if (Grado_M == 3)
          {
              //else
              //Utilizada para surtidores con 3 Manguera
              int j = 1;
              for (int i = 1; i <= Caras; i++)
              {
                  caraR = (2 * intSurtidor) - j;
                  j = j - 1;
                  //{
                  //    int intSurtidor = Convert.ToInt16(strSurtidor);
                  //    ArrayPrecios.Add("1|8010|1000|" + (intSurtidor * 1));
                  //    ArrayPrecios.Add("2|8600|8600|" + (intSurtidor * 2));
                  //    ArrayPrecios.Add("3|7400|7400|" + (intSurtidor * 3));
                  //    ArrayPrecios.Add("1|7400|7400|" + (intSurtidor * 4));
                  //    ArrayPrecios.Add("2|8500|8500|" + (intSurtidor * 5));
                  //    ArrayPrecios.Add("3|7400|7400|" + (intSurtidor * 6));   

                  //}2


                  ArrayPrecios.Add(1 + "|" + PV_MangueraG0.Text + "|" + PV_MangueraG0.Text + "|" + (Grado_M * caraR - 2));
                  ArrayPrecios.Add(2 + "|" + PV_MangueraG1.Text + "|" + PV_MangueraG1.Text + "|" + (Grado_M * caraR - 1));
                  ArrayPrecios.Add(3 + "|" + PV_MangueraG2.Text + "|" + PV_MangueraG2.Text + "|" + (Grado_M * caraR));

                  //ArrayPrecios.Add(i + "|" + PV_MangueraG0.Text + "|" + PV_MangueraG0.Text + "|" + (intSurtidor * 4));
                  //ArrayPrecios.Add(i + "|" + PV_MangueraG1.Text + "|" + PV_MangueraG1.Text + "|" + (intSurtidor * 5));
                  //ArrayPrecios.Add(i + "|" + PV_MangueraG2.Text + "|" + PV_MangueraG2.Text + "|" + (intSurtidor * 6));


              }

          }
            //Utilizada para Surtidores con 2 mangueras 

                //for (int i = 1; i <= Manguera1; i++)
                //{
                //    int intSurtidor = Convert.ToInt16(strSurtidor);
                //    ArrayPrecios.Add(i + "|10000|5678|" + (intSurtidor * i));
                //    ArrayPrecios.Add("2|9990|9565|" + (intSurtidor * 6 - 4));
                //    ArrayPrecios.Add("3|10100|7405|" + (intSurtidor * 6 - 3));
                //    ArrayPrecios.Add("1|7630|8550|" + (intSurtidor * 6 - 2));
                //    ArrayPrecios.Add("2|6740|9565|" + (intSurtidor * 6 - 1));
                //    ArrayPrecios.Add("3|11100|7405|" + (intSurtidor * 6));

                //}

            System.Array ArrayPreciosEnvio = System.Array.CreateInstance(typeof(string), ArrayPrecios.Count);
            ArrayPrecios.CopyTo(ArrayPreciosEnvio);

            string Lecturas =""; 
            if (Protocolo == 2 || Protocolo == 5)
            {
                oProtocoloGilbarco_Extended.SolicitarLecturasSurtidor(ref Lecturas, strSurtidor);

            }

            if (Protocolo == 0)
            {
                oProtocoloTokheim.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }
            if (Protocolo == 1)
            {
                oProtocoloWayne.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }
            if (Protocolo == 2 || Protocolo == 5)
            {
                oProtocoloGilbarco_Extended.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }
            if (Protocolo == 4)
            {
                //oprotocolo_EMR3.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
                //oPrptocoloPumpControl.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }

            if (Protocolo == 6)
            {
               oProtocoloDevelco.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }


            if (Protocolo == 8)
            {
                oProtocoloHongYang.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }

            if (Protocolo == 9)
            {
              //oProtocoloH2P_Safe.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }


            if (Protocolo == 11)
            {
                //oProtocoloBennett.Evento_TurnoAbierto(strSurtidor, strPuerto, ArrayPreciosEnvio);
            }



            Application.DoEvents();
        }

       

        private void groupBox7_Enter(object sender, EventArgs e)
        {

        }

        private void frmEncuestador_Load(object sender, EventArgs e)
        {

        }

        private void ECO__CheckedChanged(object sender, EventArgs e)
        {

                ECO_Loop = ECO_.Checked;
        }

        private void cmbProtocolo_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void frmEncuestador_Load_1(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
        }

        private void FactorPrecioFRM_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnCambiarDensidad_Click_1(object sender, EventArgs e)
        {

        }

        private void btnDetenerVenta_Click_1(object sender, EventArgs e)
        {

        }

        private void btnCerrarTurno_Click_1(object sender, EventArgs e)
        {

        }

        private void NumCaras_TextChanged(object sender, EventArgs e)
        {

        }

        private void Manguera_cara_TextChanged(object sender, EventArgs e)
        {

        }

        private void rdbImporte_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rdbVolumen_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            
        }

       
     


     }

     

      
      

    

       

        

       
    }


        
