using UnityEngine;

[System.Serializable]
public class WorkerAntNN
{
    public int inputSize, hiddenSize, outputSize;

    public float[,] w1; // [hidden, input]
    public float[] b1;  // [hidden]
    public float[,] w2; // [output, hidden]
    public float[] b2;  // [output]

    public WorkerAntNN(int input, int hidden, int output)
    {
        inputSize = input;
        hiddenSize = hidden;
        outputSize = output;

        w1 = new float[hiddenSize, inputSize];
        b1 = new float[hiddenSize];
        w2 = new float[outputSize, hiddenSize];
        b2 = new float[outputSize];

        Randomize();
    }

    public void Randomize(float range = 1f)
        // ------------------------------------------------------
        // HIDDEN LAYER PARAMETERS
        // ------------------------------------------------------
        // b1 = Biases for hidden neurons
        // w1 = Weights from INPUT → HIDDEN
        //
        // Shape:
        // w1[hiddenNeuron, inputNeuron]
        // ------------------------------------------------------
    {
        // Start with Random weights and biases
        // loop for amount of hidden sizes
        for (int h = 0; h < hiddenSize; h++)
        {
            // Bias 1 for hidden layer with random vals
            b1[h] = Random.Range(-range, range);
            for (int i = 0; i < inputSize; i++)
                w1[h, i] = Random.Range(-range, range);
        }

        for (int o = 0; o < outputSize; o++)
        {
            b2[o] = Random.Range(-range, range);
            for (int h = 0; h < hiddenSize; h++)
                w2[o, h] = Random.Range(-range, range);
        }
    }

    public float[] Forward(float[] x)
    {
        // ======================================================
        // INPUT VECTOR (x)
        // ======================================================
        // Example meaning in your ant sim:
        //
        // x[0] = queenDirX
        // x[1] = queenDirZ
        // x[2] = mulchDirX
        // x[3] = mulchDirZ
        // x[4] = carryingFood (0 or 1)
        // x[5] = noise / exploration
        //
        // inputSize = 6
        // ======================================================


        // ======================================================
        // HIDDEN LAYER COMPUTATION
        // ======================================================
        // Each hidden neuron:
        // sum = bias + Σ(weight * input)
        // activation = ReLU(sum)
        //
        // Hidden neurons learn patterns like:
        // "food is ahead + I’m not carrying → move forward"
        // ======================================================
        float[] h = new float[hiddenSize];

        for (int j = 0; j < hiddenSize; j++)
        {
            // Start with hidden neuron bias
            float sum = b1[j];
            for (int i = 0; i < inputSize; i++)
                // Weighted input contribution
                sum += w1[j, i] * x[i];

            // Activation function: ReLU
            // If sum < 0 → neuron off
            // If sum > 0 → neuron active
            h[j] = Mathf.Max(0f, sum);
        }

        float[] y = new float[outputSize];
        for (int o = 0; o < outputSize; o++)
        {
            float sum = b2[o];
            for (int j = 0; j < hiddenSize; j++)
                sum += w2[o, j] * h[j];
            y[o] = sum; // raw scores
        }
        return y;
    }

    public WorkerAntNN Clone()
    {
        var c = new WorkerAntNN(inputSize, hiddenSize, outputSize);

        for (int h = 0; h < hiddenSize; h++)
        {
            c.b1[h] = b1[h];
            for (int i = 0; i < inputSize; i++)
                c.w1[h, i] = w1[h, i];
        }

        for (int o = 0; o < outputSize; o++)
        {
            c.b2[o] = b2[o];
            for (int h = 0; h < hiddenSize; h++)
                c.w2[o, h] = w2[o, h];
        }
        return c;
    }

    public void Mutate(float mutationRate, float mutationStrength)
    {
        // Each weight/bias has a chance to get a small random change
        for (int h = 0; h < hiddenSize; h++)
        {
            if (Random.value < mutationRate) b1[h] += Random.Range(-mutationStrength, mutationStrength);
            for (int i = 0; i < inputSize; i++)
                if (Random.value < mutationRate) w1[h, i] += Random.Range(-mutationStrength, mutationStrength);
        }

        for (int o = 0; o < outputSize; o++)
        {
            if (Random.value < mutationRate) b2[o] += Random.Range(-mutationStrength, mutationStrength);
            for (int h = 0; h < hiddenSize; h++)
                if (Random.value < mutationRate) w2[o, h] += Random.Range(-mutationStrength, mutationStrength);
        }
    }

    public static WorkerAntNN Crossover(WorkerAntNN a, WorkerAntNN b)
    {
        // Assumes same sizes
        var c = new WorkerAntNN(a.inputSize, a.hiddenSize, a.outputSize);

        for (int h = 0; h < a.hiddenSize; h++)
        {
            c.b1[h] = (Random.value < 0.5f) ? a.b1[h] : b.b1[h];
            for (int i = 0; i < a.inputSize; i++)
                c.w1[h, i] = (Random.value < 0.5f) ? a.w1[h, i] : b.w1[h, i];
        }

        for (int o = 0; o < a.outputSize; o++)
        {
            c.b2[o] = (Random.value < 0.5f) ? a.b2[o] : b.b2[o];
            for (int h = 0; h < a.hiddenSize; h++)
                c.w2[o, h] = (Random.value < 0.5f) ? a.w2[o, h] : b.w2[o, h];
        }

        return c;
    }
}
