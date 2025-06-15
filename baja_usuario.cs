using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace FunctionApp1
{
    public class baja_usuario
    {
        class ParamBajaUsuario
        {
            public string? email;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) { this.mensaje = mensaje; }
        }

        [Function("borra_usuario")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
            HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                ParamBajaUsuario? data = JsonConvert.DeserializeObject<ParamBajaUsuario>(body);
                if (data == null || data.email == null) throw new Exception("Falta el email");

                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");

                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";
                using var conexion = new MySqlConnection(cs);
                conexion.Open();
                using var tx = conexion.BeginTransaction();

                var eliminarFoto = new MySqlCommand(
                    "DELETE FROM fotos_usuarios WHERE id_usuario=(SELECT id_usuario FROM usuarios WHERE email=@correo)",
                    conexion, tx);
                eliminarFoto.Parameters.AddWithValue("@correo", data.email);
                eliminarFoto.ExecuteNonQuery();

                var eliminarUsuario = new MySqlCommand(
                    "DELETE FROM usuarios WHERE email=@correo", conexion, tx);
                eliminarUsuario.Parameters.AddWithValue("@correo", data.email);
                eliminarUsuario.ExecuteNonQuery();

                tx.Commit();
                return new OkObjectResult("Usuario eliminado");
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(e.Message)));
            }
        }
    }
}
