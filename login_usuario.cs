using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FunctionApp1
{
    public class login_usuario
    {
        class ParamLogin
        {
            public string? email;
            public string? password;
        }

        class ResultadoLogin
        {
            public int id_usuario;
            public string token;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) => this.mensaje = mensaje;
        }

        private string GeneraToken()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 20);
        }

        [Function("login")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]
            HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                ParamLogin? datos = JsonConvert.DeserializeObject<ParamLogin>(body);

                if (datos == null || string.IsNullOrEmpty(datos.email) || string.IsNullOrEmpty(datos.password))
                    throw new Exception("Faltan datos de login");

                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");

                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";
                using var conexion = new MySqlConnection(cs);
                conexion.Open();

                var cmd = new MySqlCommand("SELECT id_usuario, password FROM usuarios WHERE email = @e", conexion);
                cmd.Parameters.AddWithValue("@e", datos.email);
                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error("Usuario no encontrado")));

                int id_usuario = reader.GetInt32(0);
                string password_en_bd = reader.GetString(1);

                if (password_en_bd != datos.password)
                    return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error("Contraseña incorrecta")));

                reader.Close();

                // Generar y guardar nuevo token
                string nuevo_token = GeneraToken();
                var cmdUpdate = new MySqlCommand("UPDATE usuarios SET token = @t WHERE id_usuario = @id", conexion);
                cmdUpdate.Parameters.AddWithValue("@t", nuevo_token);
                cmdUpdate.Parameters.AddWithValue("@id", id_usuario);
                cmdUpdate.ExecuteNonQuery();

                return new OkObjectResult(JsonConvert.SerializeObject(new ResultadoLogin
                {
                    id_usuario = id_usuario,
                    token = nuevo_token
                }));
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(e.Message)));
            }
        }
    }
}
