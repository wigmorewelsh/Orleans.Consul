# There can only be a single job definition per file. This job is named
# "example" so it will create a job with the ID and Name "example".

job "fabio" {

  datacenters = ["dc1"]

  type = "service"

  update {
    max_parallel = 1
    min_healthy_time = "10s"
    healthy_deadline = "3m"
    auto_revert = false
    canary = 0
  }

  group "cache" {
    count = 1

    restart {
      attempts = 10
      interval = "5m"

      delay = "25s"

      mode = "delay"
    }

    task "fabio" {
      # The "driver" parameter specifies the task driver that should be used to
      # run the task.
      driver = "raw_exec"

      # The "config" stanza specifies the driver configuration, which is passed
      # directly to the driver to start the task. The details of configurations
      # are specific to each driver, so please see specific driver
      # documentation for more information.
      config {
        command  = "fabio.exe"
        args = [
          "--proxy.addr", ":${NOMAD_PORT_http}"
        ]
      }

      artifact {
        source = "s3::http://127.0.0.1:9000/nomad/fabio.exe"
        options {
          aws_access_key_id     = "XOS68RHLTHGF30GTE5SM"
          aws_access_key_secret = "MNAspSp1/kcQq+sBxnk1ygIZhuwmk/JhibMzu/AO"
        }
      }

      resources {
        cpu    = 100 # 500 MHz
        memory = 25 # 256MB
        network {
          mbits = 1
          port "http" {}
        }
      }

      logs {
        max_files     = 10
        max_file_size = 10
      }

      service {
        name = "fabio"
        tags = ["global", "cache"]

        port = "http"

        check {
          type = "tcp"
          port = "http"
          interval = "10s"
          timeout  = "2s"
        }
      }
    }
  }
}