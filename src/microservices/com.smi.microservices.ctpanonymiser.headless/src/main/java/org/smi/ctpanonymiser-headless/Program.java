
import java.io.IOException;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.File;
import java.util.concurrent.TimeUnit;

public class Program {

    public static void main(String[] args) throws Exception, IOException {

        BufferedReader br = new BufferedReader(new InputStreamReader(System.in));

        String input = br.readLine();
        if(!input.startsWith("INIT"))
            throw new Exception("Did not receive INIT as first message");
        
        String anonFilePath = input.substring(5);
        
        File anonScriptFile = new File(anonFilePath);
        if (!anonScriptFile.exists()) {
            System.out.println("Could not find anonymisation script");
            System.out.println("BYE");
            return;
        }

        System.out.println("READY");

        while(true) {
            try {

                input = br.readLine();

                if(input.equals("EXIT"))
                    break;

                TimeUnit.SECONDS.sleep(5);

            } catch (IOException ioe) {
                System.out.println(ioe);
                break;
            }
        }

        System.out.println("BYE");
    }
}
