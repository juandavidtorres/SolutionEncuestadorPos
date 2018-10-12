Public Interface IVeederRoot
    'Eventos que escucha la Veeder root
    Sub Evento_InformarStocksTanques()
    Sub Evento_InformarStocksTanquesCierreTurno(ByRef IdTurno As Integer)
    Sub ExisteComunicacionVeederRootReciboCombustible(ByRef ExisteComunicacion As Boolean)
    Sub Evento_ObtenerSaldoTanqueAjusteTurno(ByRef Tanques As ColTanques)
    Sub Evento_InformarStocksTanquesCierreTurnoServicio(ByRef idTurno As Integer)

    'Eventos que lanza la Veeder root
    Event EnviarInformacionTanques(ByRef informacionTanques As ColTanques)
    Event EnviarInformacionTanquesCierreTurno(ByRef informacionTanques As ColTanques, ByRef IdTurno As Integer)
    Event ReportarAlarmasVariablesTanques(ByRef informacionTanques As ColTanques)
    Event EnviarInformacionTanquesCierreTurnoServicio(ByRef informacionTanques As ColTanques, ByRef IdTurno As Integer)

   
End Interface
