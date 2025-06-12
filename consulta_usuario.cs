// (c) Carlos Pineda Guerrero. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net;
namespace FunctionApp1
{
    public class consulta_usuario
    {
        class Usuario
        {
            public int? id_usuario;
            public string? email;
            public string? nombre;
            public string? apellido_paterno;
            public string? apellido_materno;
            public DateTime? fecha_nacimiento;
            public long? telefono;
            public string? genero;
            public string? foto;  // foto en base 64
        }
        class ParamConsultaUsuario
        {
            public string? email;
        }
        class Error
        {
            public string mensaje;
            public Error(string mensaje)
            {
                this.mensaje = mensaje;
            }
        }
        [Function("consulta_usuario")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get","post")]
            HttpRequest req)
        {
            try
            {
                string? email = req.Query["email"];
                if (email == null)
                {
                    string body = await new StreamReader(req.Body).ReadToEndAsync();
                    ParamConsultaUsuario? data = JsonConvert.DeserializeObject<ParamConsultaUsuario>(body);
                    if (data == null || data.email == null) throw new Exception("Se esperan el email");
                    email = data.email;
                }
                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");
                string cs = "Server=" + Server + ";UserID=" + UserID + ";Password=" + Password + ";" + "Database=" + Database + ";SslMode=Preferred;";
                var conexion = new MySqlConnection(cs);
                conexion.Open();
                try
                {
                    var cmd = new MySqlCommand("SELECT a.id_usuario,a.email,a.nombre," +
                                                "a.apellido_paterno,a.apellido_materno," +
                                                "a.fecha_nacimiento,a.telefono,a.genero," +
                                                "b.foto,length(b.foto) " +
                                                "FROM usuarios a LEFT OUTER JOIN fotos_usuarios b ON a.id_usuario=b.id_usuario " +
                                                "WHERE a.email=@email");
                    cmd.Connection = conexion;
                    cmd.Parameters.AddWithValue("@email", email);
                    MySqlDataReader r = cmd.ExecuteReader();
                    try
                    {
                        if (!r.Read())
                            throw new Exception("El email no existe");
                        var usuario_foto = new Usuario();
                        usuario_foto.id_usuario = r.GetInt32(0);
                        usuario_foto.email = r.GetString(1);
                        usuario_foto.nombre = r.GetString(2);
                        usuario_foto.apellido_paterno = r.GetString(3);
                        usuario_foto.apellido_materno = !r.IsDBNull(4) ? r.GetString(4) : null;
                        usuario_foto.fecha_nacimiento = r.GetDateTime(5);
                        usuario_foto.telefono = !r.IsDBNull(6) ? r.GetInt64(6) : null;
                        usuario_foto.genero = !r.IsDBNull(7) ? r.GetString(7) : null;
                        if (!r.IsDBNull(8))
                        {
                            var longitud = r.GetInt32(9);
                            byte[] foto = new byte[longitud];
                            r.GetBytes(8, 0, foto, 0, longitud);
                            usuario_foto.foto = Convert.ToBase64String(foto);
                        }
                        return new OkObjectResult(JsonConvert.SerializeObject(usuario_foto));
                    }
                    finally
                    {
                        r.Close();
                    }
                }
                finally
                {
                    conexion.Close();
                }
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(e.Message)));
            }
        }
    }
}