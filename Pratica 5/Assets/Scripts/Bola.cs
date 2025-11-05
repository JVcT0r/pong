using UnityEngine;
using TMPro;

public class Bola : MonoBehaviour
{
    public float velocidade = 6f;
    private Rigidbody2D rb;

    public int pontosEsquerda = 0;
    public int pontosDireita = 0;

    public TMP_Text textoPontosEsquerda;
    public TMP_Text textoPontosDireita;

    private bool emJogo = false;
    private bool jogoComecou = false; // agora a bola só anda depois do START

    private UdpClientFourPlayers udpClient;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        udpClient = FindFirstObjectByType<UdpClientFourPlayers>();

        rb.linearVelocity = Vector2.zero;
        transform.position = Vector3.zero;
    }

    void Update()
    {
        if (udpClient == null) return;

        // ⚠️ só começa se o servidor enviou "START"
        jogoComecou = (bool)udpClient.GetType().GetField("jogoComecou", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(udpClient);

        if (!jogoComecou)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = Vector3.zero;
            return;
        }

        // Só o Player 1 pode lançar a bola e enviar posição
        if (!emJogo && udpClient.myId == 1)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                LançarBola();
        }

        // Envia a posição da bola (apenas o Player 1)
        if (udpClient.myId == 1)
        {
            string msgBall = $"BALL:{transform.position.x:F2};{transform.position.y:F2}";
            udpClient.SendUdpMessage(msgBall);
        }
    }

    void LançarBola()
    {
        emJogo = true;
        float x = Random.Range(0, 2) == 0 ? -1f : 1f;
        float y = Random.Range(-0.5f, 0.5f);
        Vector2 dir = new Vector2(x, y).normalized;
        rb.linearVelocity = dir * velocidade;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // rebate nas raquetes e paredes
        if (collision.collider.CompareTag("Raquete") || collision.collider.CompareTag("Parede"))
        {
            rb.linearVelocity = rb.linearVelocity.normalized * velocidade;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!jogoComecou) return;

        // 🟦 Gol no lado direito
        if (other.CompareTag("Gol2"))
        {
            pontosEsquerda++;
            AtualizarPontuacao();
            ReiniciarBola();

            // apenas Player 1 envia SCORE
            if (udpClient.myId == 1)
            {
                string msgScore = $"SCORE:{pontosEsquerda};{pontosDireita}";
                udpClient.SendUdpMessage(msgScore);
            }
        }
        // 🟥 Gol no lado esquerdo
        else if (other.CompareTag("Gol1"))
        {
            pontosDireita++;
            AtualizarPontuacao();
            ReiniciarBola();

            if (udpClient.myId == 1)
            {
                string msgScore = $"SCORE:{pontosEsquerda};{pontosDireita}";
                udpClient.SendUdpMessage(msgScore);
            }
        }
    }

    public void AtualizarPontuacao()
    {
        if (textoPontosEsquerda != null)
            textoPontosEsquerda.text = pontosEsquerda.ToString();
        if (textoPontosDireita != null)
            textoPontosDireita.text = pontosDireita.ToString();
    }

    void ReiniciarBola()
    {
        emJogo = false;
        rb.linearVelocity = Vector2.zero;
        transform.position = Vector3.zero;
    }
}
