from ruamel.yaml import YAML
import subprocess
import os
import time
import datetime

yaml = YAML()


WORK_DIR = r"C:\LEO\SEB\SEB8\BachelorArbeit\ParkourMLAgent"
ENV_PATH = r'Builds\NewV3AllLevelDrcOcNormalx2\ParkourMLAgent.exe'
RUN_ID = "Skript_Showcase"
BASED_ON_ID = "" 

MAX_TOTAL_STEPS = 1_000_000_000
STEP_INCREMENT = 1_000_000
LR_INCREMENT = 0.0001  
LOG_DIR = os.path.join(WORK_DIR, r"config\Automation\ConfigLog")
LOG_FILE_PATH = os.path.join(LOG_DIR, RUN_ID + "Log.txt")

CONFIG_FILE_DEFAULT = os.path.join(WORK_DIR, r"config\Automation\Default.yaml")
CONFIG_DIR = os.path.join(WORK_DIR, r"config\Automation")
CONFIG_FILE = os.path.join(CONFIG_DIR, RUN_ID + "config.yaml")


if not os.path.exists(CONFIG_FILE):
    os.makedirs(CONFIG_DIR, exist_ok=True)
    with open(CONFIG_FILE_DEFAULT, 'r') as src, open(CONFIG_FILE, 'w') as dst:
        dst.write(src.read())
    print(f"Neue Konfigurationsdatei erstellt: {CONFIG_FILE}")
else:
    print(f"Bestehende Konfigurationsdatei wird verwendet: {CONFIG_FILE}")



def log_training_parameters(steps, lr, beta, epsilon, run_id=None, based_on_id=None):
    os.makedirs(LOG_DIR, exist_ok=True)
    timestamp = datetime.datetime.now().strftime("[%Y-%m-%d %H:%M:%S]")
    with open(LOG_FILE_PATH, "a", encoding="utf-8") as log_file:
        log_file.write(f"{timestamp} Neues Training mit max_steps={steps}, learning_rate={lr}, beta={beta}, epsilon={epsilon}\n")
        if run_id:
            log_file.write(f"{timestamp} Aktuelle Trainings-ID: {run_id}\n")
        if based_on_id:
            log_file.write(f"{timestamp} Basierend auf Trainings-ID: {based_on_id}\n")
        log_file.write("\n")

def load_config(path):
    with open(path, 'r') as f:
        return yaml.load(f)

def save_config(config, path):
    with open(path, 'w') as f:
        yaml.dump(config, f)

def get_behavior_key(config):
    return next(iter(config['behaviors']))

def get_current_values(config):
    key = get_behavior_key(config)
    behavior = config['behaviors'][key]
    max_steps = behavior['max_steps']
    lr = behavior['hyperparameters']['learning_rate']
    beta = behavior['hyperparameters']['beta']
    epsilon = behavior['hyperparameters']['epsilon']
    return max_steps, lr, beta, epsilon

def calculate_increment(max_steps, lr_base=0.00008, beta_eps_base=0.003):
    if max_steps < 3_000_000:
        return lr_base, beta_eps_base
    elif max_steps < 5_000_000:
        return 0.00006, 0.002
    elif max_steps < 7_000_000:
        return 0.00004, 0.001
    elif max_steps < 9_000_000:
        return 0.00002, 0.0009
    elif max_steps < 11_000_000:
        return 0.00001, 0.0008
    elif max_steps < 15_000_000:
        return 0.000005, -0.001
    elif max_steps < 30_000_000:
        return 0.0, 0.0
    else:
        return 0.0, 0

def update_config(config, new_steps, new_lr, new_beta, new_epsilon):
    key = get_behavior_key(config)
    hyperparams = config['behaviors'][key]['hyperparameters']
    
    config['behaviors'][key]['max_steps'] = new_steps
    hyperparams['learning_rate'] = new_lr
    hyperparams['beta'] = new_beta
    hyperparams['epsilon'] = new_epsilon

    return config

def run_training(use_resume=False, initialize_from=None):
    resume_flag = "--resume" if use_resume else ""
    init_flag = f"--initialize-from={initialize_from}" if initialize_from else ""
    cmd = f'mlagents-learn "{CONFIG_FILE}" --run-id={RUN_ID} --env="{ENV_PATH}" {resume_flag} {init_flag}'
    print(f" Starte Kommando: {cmd}")
    process = subprocess.Popen(cmd, cwd=WORK_DIR, shell=True)

    try:
        while process.poll() is None:
            print("Training läuft noch... (warte 120 Sekunden)")
            time.sleep(120)
    except KeyboardInterrupt:
        print("Manuell abgebrochen. Prozess wird beendet...")
        process.terminate()

    print("Training abgeschlossen.\n")

def main():
    first_run = True      
    continue_training = False  
    based_on = False

    for _ in range(0, MAX_TOTAL_STEPS, STEP_INCREMENT):
        config = load_config(CONFIG_FILE)
        prev_steps, prev_lr, prev_beta, prev_epsilon = get_current_values(config)

        if continue_training:
            print(f"Training wird fortgesetzt bei max_steps={prev_steps} (keine Änderungen an Parametern)")
            log_training_parameters(prev_steps, prev_lr, prev_beta, prev_epsilon)
            run_training(use_resume=True)
            continue_training = False
            continue  

        if not first_run:
            lr_inc, beta_eps_inc = calculate_increment(prev_steps)
            new_steps = prev_steps + STEP_INCREMENT
            new_lr = round(prev_lr + lr_inc, 10)
            new_beta = round(prev_beta + (beta_eps_inc*0.8), 10)
            new_epsilon = round(prev_epsilon + beta_eps_inc, 10)

            print(f"Neues Training mit max_steps={new_steps}, learning_rate={new_lr}, beta={new_beta}, epsilon={new_epsilon}")
            log_training_parameters(new_steps, new_lr, new_beta, new_epsilon)
            updated_config = update_config(config, new_steps, new_lr, new_beta, new_epsilon)
            save_config(updated_config, CONFIG_FILE)
            run_training(use_resume=True)
        else:
            print(f"Erster Trainingslauf mit max_steps={prev_steps}")
            init_from = BASED_ON_ID if based_on else None
            log_training_parameters(
                prev_steps, prev_lr, prev_beta, prev_epsilon,
                run_id=RUN_ID,
                based_on_id=BASED_ON_ID if based_on else None
            )
            run_training(use_resume=False, initialize_from=init_from)

        first_run = False
        based_on = False
        

if __name__ == "__main__":
    main()



#  venv\Scripts\activate

#  tensorboard --logdir results
#  python new_automation.py
#  python Training_Automation.py


