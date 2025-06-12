// (c) Carlos Pineda Guerrero. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
namespace FunctionApp1
{
    public class borra_usuario
    {
        class ParamBorraUsuario
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
        [Function("borra_usuario")]
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
                    ParamBorraUsuario? data = JsonConvert.DeserializeObject<ParamBorraUsuario>(body);
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
                MySqlTransaction transaccion = conexion.BeginTransaction();
                try
                {
                    var cmd_2 = new MySqlCommand();
                    cmd_2.Connection = conexion;
                    cmd_2.Transaction = transaccion;
                    cmd_2.CommandText = "DELETE FROM fotos_usuarios WHERE id_usuario=(SELECT id_usuario FROM usuarios WHERE email=@email)";
                    cmd_2.Parameters.AddWithValue("@email", email);
                    cmd_2.ExecuteNonQuery();
                    var cmd_3 = new MySqlCommand();
                    cmd_3.Connection = conexion;
                    cmd_3.Transaction = transaccion;
                    cmd_3.CommandText = "DELETE FROM usuarios WHERE email=@email";
                    cmd_3.Parameters.AddWithValue("@email", email);
                    cmd_3.ExecuteNonQuery();
                    transaccion.Commit();
                    return new OkObjectResult("Se modificó el usuario");
                }
                catch (Exception e)
                {
                    transaccion.Rollback();
                    throw new Exception(e.Message);
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