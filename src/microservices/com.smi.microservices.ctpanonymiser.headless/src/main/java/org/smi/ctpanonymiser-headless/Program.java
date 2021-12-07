import java.io.IOException;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.File;

public class Program {

    public static void main(String[] args) throws Exception, IOException {

        String anonFilePath = args[0];
        File anonScriptFile = new File(anonFilePath);

        if (!anonScriptFile.exists()) {
            System.out.println("Could not find anonymisation script. BYE");
            return;
        }

        BufferedReader br = new BufferedReader(new InputStreamReader(System.in));

        String input = br.readLine();
        if (!input.startsWith("PING"))
            throw new Exception("Did not receive PING as first message");
        System.out.println("PONG");

        while (true) {
            try {

                input = br.readLine();

                if (input.equals("EXIT"))
                    break;

                if (input.startsWith("ANON")) {
                    String[] parts = input.split("\\|");
                    String sourceFile = parts[1];
                    String destFile = parts[2];

                    if (sourceFile.equals("foo"))
                        System.out.println("ANON_OK " + destFile);
                    else
                        System.out.println("oh no!");

                    continue;
                }

                System.out.println("Unknown command '" + input + "'");
                break;

            } catch (IOException ioe) {
                System.out.println(ioe);
                break;
            }
        }

        System.out.println("BYE");
    }
}
